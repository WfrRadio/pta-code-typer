// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace PTA.CodeTyper.App;

/// <summary>
/// 应用程序主窗口。
/// </summary>
public sealed partial class MainWindow : Window
{
    public AppViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        Title = "PTA Code Typer";

        // 应用 Mica 背景
        try
        {
            SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
        }
        catch
        {
            // Windows 10 或旧版本回退
        }

        // 设置窗口基础样式
        ExtendsContentIntoTitleBar = false;

        // 设置初始窗口大小
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow?.Resize(new global::Windows.Graphics.SizeInt32(960, 680));
        }
        catch
        {
        }

        // 处理运行中关闭窗口
        Closed += OnClosed;
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        if (NavView.MenuItems.Count > 0)
        {
            NavView.SelectedItem = NavView.MenuItems[0];
        }
        ContentFrame.Navigate(typeof(CodeInputPage), ViewModel);
    }

    private OverlappedPresenter? GetPresenter()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id)?.Presenter as OverlappedPresenter;
    }

    public void MinimizeForTyping() => GetPresenter()?.Minimize();

    public void RestoreAfterTyping()
    {
        GetPresenter()?.Restore();
        Activate();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            var tag = item.Tag?.ToString();
            var pageType = tag switch
            {
                "code" => typeof(CodeInputPage),
                "environment" => typeof(EnvironmentPage),
                "activity" => typeof(ActivityPage),
                _ => typeof(CodeInputPage)
            };
            ContentFrame.Navigate(pageType, ViewModel);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        if (ViewModel.IsBusy)
        {
            args.Handled = true;
            ViewModel.CancelCommand.Execute(null);
            // 任务结束后关闭
            ViewModel.PendingClose = true;
        }
    }
}
