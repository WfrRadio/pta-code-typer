// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using PTA.CodeTyper.Core;
using PTA.CodeTyper.Windows;
using global::Windows.ApplicationModel.DataTransfer;
using global::Windows.Storage.Pickers;
using WinRT.Interop;

namespace PTA.CodeTyper.App;

/// <summary>
/// 应用程序主视图模型，驱动所有界面状态与输入引擎生命周期。
/// </summary>
public sealed class AppViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    // 代码输入与设置状态
    private string _code = string.Empty;
    private string _sourceName = "尚未载入代码";
    private string _browser = "edge";
    private double _delay = 0.045;
    private int _countdown = 7;
    private int _checkEvery = 1;

    // 运行期状态
    private bool _isBusy;
    private double _progress;
    private string _status = "准备好代码，就可以开始。";
    private string _statusIcon = "\uE735"; // 空心圆圈
    private bool _hasFailed;

    /// <summary>
    /// 当任务正在运行中用户请求关闭窗口时设为 true，在任务结束时退出。
    /// </summary>
    public bool PendingClose { get; set; }

    public string Code
    {
        get => _code;
        set
        {
            if (SetField(ref _code, value))
            {
                OnPropertyChanged(nameof(CanStart));
                OnPropertyChanged(nameof(LineCount));
                OnPropertyChanged(nameof(CharCount));
                OnPropertyChanged(nameof(StatsText));
                StartCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SourceName
    {
        get => _sourceName;
        set => SetField(ref _sourceName, value);
    }

    public string Browser
    {
        get => _browser;
        set => SetField(ref _browser, value);
    }

    public double Delay
    {
        get => _delay;
        set => SetField(ref _delay, value);
    }

    public int Countdown
    {
        get => _countdown;
        set => SetField(ref _countdown, value);
    }

    public int CheckEvery
    {
        get => _checkEvery;
        set => SetField(ref _checkEvery, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetField(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanStart));
                OnPropertyChanged(nameof(IsNotBusy));
                OnPropertyChanged(nameof(BusyVisibility));
                OnPropertyChanged(nameof(NotBusyVisibility));
                StartCommand.RaiseCanExecuteChanged();
                CancelCommand.RaiseCanExecuteChanged();
                LoadClipboardCommand.RaiseCanExecuteChanged();
                ImportFileCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public double Progress
    {
        get => _progress;
        set
        {
            if (SetField(ref _progress, value))
            {
                OnPropertyChanged(nameof(StatusBrush));
            }
        }
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public string StatusIcon
    {
        get => _statusIcon;
        set => SetField(ref _statusIcon, value);
    }

    public bool HasFailed
    {
        get => _hasFailed;
        set
        {
            if (SetField(ref _hasFailed, value))
            {
                OnPropertyChanged(nameof(StatusBrush));
            }
        }
    }

    public bool CanStart => !IsBusy && !string.IsNullOrWhiteSpace(Code);
    public bool IsNotBusy => !IsBusy;

    public int LineCount => string.IsNullOrEmpty(Code) ? 0 : Code.Split('\n').Length;
    public int CharCount => Code.EnumerateRunes().Count();
    public string StatsText => $"{CharCount} 字符 · {LineCount} 行";

    public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NotBusyVisibility => IsBusy ? Visibility.Collapsed : Visibility.Visible;
    public Visibility EmptyLogsVisibility => Logs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility LogsListVisibility => Logs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public SolidColorBrush StatusBrush
    {
        get
        {
            if (HasFailed)
                return new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
            if (Progress >= 1.0)
                return new SolidColorBrush(Microsoft.UI.Colors.ForestGreen);
            return new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
        }
    }

    public string Version => "0.1.2";

    // 下拉框选项数据源
    public BrowserOption[] BrowserOptions { get; } =
    [
        new("edge", "Microsoft Edge"),
        new("chrome", "Google Chrome"),
    ];

    public DelayOption[] DelayOptions { get; } =
    [
        new(0.045, "标准 · 45 ms"),
        new(0.070, "稳妥 · 70 ms"),
        new(0.025, "较快 · 25 ms"),
    ];

    public CountdownOption[] CountdownOptions { get; } =
    [
        new(5, "5 秒"),
        new(7, "7 秒"),
        new(10, "10 秒"),
        new(15, "15 秒"),
        new(20, "20 秒"),
    ];

    public CheckEveryOption[] CheckEveryOptions { get; } =
    [
        new(1, "逐行核对"),
        new(5, "每 5 行核对"),
    ];

    // 活动日志列表
    public ObservableCollection<LogEntry> Logs { get; } = new();

    // 命令
    public RelayCommand StartCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand LoadClipboardCommand { get; }
    public RelayCommand ImportFileCommand { get; }
    public RelayCommand OpenReportsCommand { get; }

    // 报告目录路径
    public string ReportsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PTA Code Typer", "reports");

    private CancellationControl? _currentControl;
    private DispatcherQueue? _dispatcher;

    public AppViewModel()
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        StartCommand = new RelayCommand(Start, () => CanStart);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
        LoadClipboardCommand = new RelayCommand(LoadClipboard, () => !IsBusy);
        ImportFileCommand = new RelayCommand(ImportFile, () => !IsBusy);
        OpenReportsCommand = new RelayCommand(OpenReports);
    }

    /// <summary>
    /// 从系统剪贴板载入代码文本。
    /// </summary>
    public async void LoadClipboard()
    {
        if (IsBusy) return;

        try
        {
            var package = Clipboard.GetContent();
            if (package != null && package.Contains(StandardDataFormats.Text))
            {
                string text = await package.GetTextAsync();
                if (string.IsNullOrWhiteSpace(text))
                {
                    ShowError("剪贴板中没有代码文本。");
                    return;
                }

                if (Encoding.UTF8.GetByteCount(text) > 256 * 1024)
                {
                    ShowError("代码超过 256 KB，请缩小内容。");
                    return;
                }

                Code = text;
                SourceName = "来自剪贴板";
                Status = "代码已载入，可编辑或直接开始。";
                StatusIcon = "\uE8A5"; // 文档图标
                HasFailed = false;
                AddLog("已从剪贴板载入代码。");
            }
            else
            {
                ShowError("剪贴板中没有纯文本代码。");
            }
        }
        catch (Exception ex)
        {
            ShowError($"读取剪贴板失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 弹出文件选择对话框并导入 UTF-8 代码文件。
    /// </summary>
    public async void ImportFile()
    {
        if (IsBusy) return;

        try
        {
            var picker = new FileOpenPicker();
            var window = App.CurrentWindow;
            if (window != null)
            {
                var hwnd = WindowNative.GetWindowHandle(window);
                InitializeWithWindow.Initialize(picker, hwnd);
            }

            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".c");
            picker.FileTypeFilter.Add(".cpp");
            picker.FileTypeFilter.Add(".h");
            picker.FileTypeFilter.Add(".hpp");
            picker.FileTypeFilter.Add(".java");
            picker.FileTypeFilter.Add(".py");
            picker.FileTypeFilter.Add(".cs");
            picker.FileTypeFilter.Add(".txt");
            picker.FileTypeFilter.Add("*");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                await LoadFileAsync(file.Path);
            }
        }
        catch (Exception ex)
        {
            ShowError($"打开文件选择器失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 读取并校验指定路径的代码文件。
    /// </summary>
    public async Task LoadFileAsync(string filePath)
    {
        if (IsBusy) return;

        try
        {
            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                ShowError("文件不存在。");
                return;
            }

            if (fileInfo.Length > 256 * 1024)
            {
                ShowError("文件超过 256 KB，请选择代码文件。");
                return;
            }

            byte[] bytes = await File.ReadAllBytesAsync(filePath);
            if (bytes.Length > 256 * 1024)
            {
                ShowError("文件超过 256 KB，请选择代码文件。");
                return;
            }

            var utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            string text;
            try
            {
                text = utf8Strict.GetString(bytes);
            }
            catch
            {
                ShowError("请将文件保存为 UTF-8 后再导入。");
                return;
            }

            if (text.StartsWith("\uFEFF"))
            {
                text = text[1..];
            }

            Code = text;
            SourceName = Path.GetFileName(filePath);
            Status = "代码已载入，可编辑或直接开始。";
            StatusIcon = "\uE8A5";
            HasFailed = false;
            AddLog($"已导入文件：{SourceName}");
        }
        catch (Exception ex)
        {
            ShowError($"无法读取文件：{ex.Message}");
        }
    }

    /// <summary>
    /// 开始键入任务。
    /// </summary>
    public void Start()
    {
        if (!CanStart) return;

        byte[] utf8Bytes = Encoding.UTF8.GetBytes(Code);
        if (utf8Bytes.Length > 256 * 1024)
        {
            ShowError("代码超过 256 KB，请缩小内容。");
            return;
        }

        IsBusy = true;
        HasFailed = false;
        Progress = 0;
        StatusIcon = "\uE916"; // 定时器图标
        Status = $"请在 {Countdown} 秒内点击 PTA 作答代码框。";
        AddLog("开始输入；请切换英文输入法，在倒计时内点击 PTA 代码框。");

        var options = new Options
        {
            Delay = Delay,
            Countdown = Countdown,
            CheckEvery = CheckEvery,
            Retries = 1,
            Browser = Browser
        };

        _currentControl = new CancellationControl();
        var control = _currentControl;
        var codeToType = Code;
        var reportsDir = ReportsDirectory;

        App.CurrentWindow?.MinimizeForTyping();

        // WinUI objects must only be accessed on the UI thread.
        var stopMonitor = new StopMonitor(control);
        stopMonitor.RegisterStopKey(nint.Zero); // Independent F8 polling; no window-thread registration.
        Task.Run(async () =>
        {
            IInputBackend? backend = null;
            try
            {
                Directory.CreateDirectory(reportsDir);
                backend = new WindowsInputBackend(control);
                var result = await TypingEngine.RunTypingAsync(backend, codeToType, options, reportsDir, Emit, countdown: true);
                Emit("done", result);
            }
            catch (CancelledException ex)
            {
                Emit("cancelled", ex.Message);
            }
            catch (StoppedException ex)
            {
                Emit("error", ex.Message);
            }
            catch (Exception ex)
            {
                Emit("error", $"输入过程发生异常：{ex.Message}");
            }
            finally
            {
                try
                {
                    stopMonitor?.Dispose();
                }
                catch
                {
                }

                try
                {
                    backend?.Dispose();
                }
                catch
                {
                }
                Emit("finished", null);
            }
        });
    }

    /// <summary>
    /// 取消当前正在执行的键入任务。
    /// </summary>
    public void Cancel()
    {
        if (!IsBusy) return;
        _currentControl?.Cancel();
        Status = "正在安全停止…";
    }

    /// <summary>
    /// 在 Windows 资源管理器中打开报告与备份文件夹。
    /// </summary>
    public void OpenReports()
    {
        try
        {
            Directory.CreateDirectory(ReportsDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = ReportsDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowError($"无法打开记录目录：{ex.Message}");
        }
    }

    /// <summary>
    /// 调度后台引擎发出的事件到 WinUI 界面线程。
    /// </summary>
    private void Emit(string eventName, object? value)
    {
        void Handler()
        {
            switch (eventName)
            {
                case "countdown":
                    Status = $"{value} 秒后开始，请点击 PTA 作答代码框。";
                    StatusIcon = "\uE916";
                    break;
                case "target":
                    if (value is IDictionary<string, object?> targetDict)
                    {
                        var bName = targetDict.TryGetValue("browser", out var b) ? b?.ToString() : "浏览器";
                        AddLog($"已锁定 {bName} 中的 PTA 编辑框。");
                    }
                    else if (value is string targetStr)
                    {
                        AddLog($"已锁定目标：{targetStr}");
                    }
                    break;
                case "progress":
                    if (value is IDictionary<string, object?> progDict)
                    {
                        int line = progDict.TryGetValue("line", out var l) && l is int li ? li : 0;
                        int total = progDict.TryGetValue("total", out var t) && t is int ti ? ti : 1;
                        Progress = (double)line / Math.Max(total, 1);
                        Status = $"已输入 {line} / {total} 行，正在核对。";
                        StatusIcon = "\uE765"; // 键盘图标
                    }
                    break;
                case "backup":
                    AddLog($"原内容已备份：{value}");
                    break;
                case "warning":
                    AddLog(value?.ToString() ?? "");
                    break;
                case "attempt":
                    if (value is IDictionary<string, object?> attDict && attDict.TryGetValue("attempt", out var att))
                    {
                        AddLog($"第 {att} 次输入。");
                    }
                    break;
                case "report":
                    AddLog($"校验记录：{value}");
                    break;
                case "done":
                    if (value is TypingResult tr)
                    {
                        Status = $"完成：{tr.Characters} 个字符，全文回读一致。";
                        StatusIcon = "\uE73E"; // 对勾图标
                        Progress = 1.0;
                        HasFailed = false;
                        AddLog(Status + " 未提交答案。");
                    }
                    else if (value is IDictionary<string, object?> doneDict)
                    {
                        int chars = doneDict.TryGetValue("characters", out var c) && c is int ci ? ci : 0;
                        Status = $"完成：{chars} 个字符，全文回读一致。";
                        StatusIcon = "\uE73E";
                        Progress = 1.0;
                        HasFailed = false;
                        AddLog(Status + " 未提交答案。");
                    }
                    break;
                case "error":
                    ShowError(value?.ToString() ?? "输入组件发生错误。");
                    break;
                case "cancelled":
                    Status = value?.ToString() ?? "已停止。";
                    StatusIcon = "\uE71A"; // 停止图标
                    AddLog(Status);
                    break;
                case "finished":
                    Finish();
                    break;
            }
        }

        if (_dispatcher != null && !_dispatcher.HasThreadAccess)
        {
            _dispatcher.TryEnqueue(Handler);
        }
        else
        {
            Handler();
        }
    }

    private void Finish()
    {
        IsBusy = false;
        StartCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
        LoadClipboardCommand.RaiseCanExecuteChanged();
        ImportFileCommand.RaiseCanExecuteChanged();

        if (PendingClose)
        {
            App.CurrentWindow?.Close();
        }
        else
        {
            App.CurrentWindow?.RestoreAfterTyping();
        }
        _currentControl?.Dispose();
        _currentControl = null;
    }

    public void AddLog(string message, bool isError = false)
    {
        Logs.Add(new LogEntry(message, isError));
        OnPropertyChanged(nameof(EmptyLogsVisibility));
        OnPropertyChanged(nameof(LogsListVisibility));
    }

    public void ShowError(string message)
    {
        Status = message;
        StatusIcon = "\uE783"; // 警告图标
        HasFailed = true;
        AddLog(message, isError: true);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// 运行活动日志项。
/// </summary>
public sealed record LogEntry(string Message, bool IsError)
{
    public DateTime Timestamp { get; } = DateTime.Now;
    public string TimeString => Timestamp.ToString("HH:mm:ss");
    public string IconGlyph => IsError ? "\uE783" : "\uE73E";
    public SolidColorBrush ColorBrush => IsError
        ? new SolidColorBrush(Microsoft.UI.Colors.OrangeRed)
        : new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
}

/// <summary>
/// 浏览器下拉选项条目。
/// </summary>
public sealed record BrowserOption(string Key, string Label);

/// <summary>
/// 字符间隔下拉选项条目。
/// </summary>
public sealed record DelayOption(double Value, string Label);

/// <summary>
/// 倒计时下拉选项条目。
/// </summary>
public sealed record CountdownOption(int Value, string Label);

/// <summary>
/// 校验频率下拉选项条目。
/// </summary>
public sealed record CheckEveryOption(int Value, string Label);

/// <summary>
/// 通用 RelayCommand 命令实现。
/// </summary>
public class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;
    public event EventHandler? CanExecuteChanged;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
