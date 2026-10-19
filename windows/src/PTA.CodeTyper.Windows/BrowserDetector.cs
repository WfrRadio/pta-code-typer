// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using PTA.CodeTyper.Core;

namespace PTA.CodeTyper.Windows;

/// <summary>
/// 浏览器前台窗口识别与基于 UI Automation (UIA) 的地址栏网址读取器。
/// </summary>
public sealed class BrowserDetector
{
    // 已知支持的浏览器进程名称及其显示名称
    private static readonly Dictionary<string, string> KnownBrowsers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["msedge"] = "Microsoft Edge",
        ["chrome"] = "Google Chrome",
        ["brave"] = "Brave",
        ["chromium"] = "Chromium",
    };

    // 浏览器配置标识到进程名称的映射
    private static readonly Dictionary<string, string> ConfigToProcess = new(StringComparer.OrdinalIgnoreCase)
    {
        ["edge"] = "msedge",
        ["chrome"] = "chrome",
        ["brave"] = "brave",
        ["chromium"] = "chromium",
    };

    /// <summary>
    /// 识别当前前台窗口是否为受支持的浏览器，并校验选定的浏览器配置。
    /// </summary>
    /// <param name="selected">用户选定的浏览器名称（如 "auto"、"edge"、"chrome"）。</param>
    /// <returns>返回包含窗口句柄、进程 ID、进程名称及浏览器显示名称的元组。</returns>
    /// <exception cref="StoppedException">当前台非支持浏览器或与选定浏览器不符时抛出。</exception>
    public static (nint Hwnd, uint Pid, string ProcessName, string BrowserLabel) GetForegroundBrowser(string? selected)
    {
        nint hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == nint.Zero || !NativeMethods.IsWindow(hwnd))
        {
            throw new StoppedException("无法确认前台应用，已停止。");
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0)
        {
            throw new StoppedException("前台进程已退出或无法识别，未执行输入。");
        }

        string processName;
        try
        {
            using var proc = Process.GetProcessById((int)pid);
            processName = proc.ProcessName;
        }
        catch (Exception ex)
        {
            throw new StoppedException($"无法识别前台进程（PID {pid}）：{ex.Message}");
        }

        if (!KnownBrowsers.TryGetValue(processName, out var browserLabel))
        {
            throw new StoppedException($"前台不是支持的浏览器（检测到 {processName}，PID {pid}）。请在倒计时内点击 PTA 代码框。");
        }

        if (!string.IsNullOrWhiteSpace(selected) && !string.Equals(selected, "auto", StringComparison.OrdinalIgnoreCase))
        {
            string expectedProcess = ConfigToProcess.TryGetValue(selected, out var mapped) ? mapped : selected;
            if (!string.Equals(processName, expectedProcess, StringComparison.OrdinalIgnoreCase))
            {
                throw new StoppedException($"当前前台是 {browserLabel}，与选定的浏览器不符。");
            }
        }

        return (hwnd, pid, processName, browserLabel);
    }

    /// <summary>
    /// 使用 UI Automation 读取指定浏览器窗口的地址栏 URL。
    /// 在专用 MTA 线程上执行，并设置 5 秒超时保护与 COMException 容错。
    /// </summary>
    /// <param name="hwnd">浏览器窗口句柄。</param>
    /// <returns>规范化后的完整 URL 字符串（包含协议头）。</returns>
    /// <exception cref="StoppedException">当读取失败或超时时抛出。</exception>
    public static string GetBrowserUrl(nint hwnd)
    {
        if (hwnd == nint.Zero || !NativeMethods.IsWindow(hwnd))
        {
            throw new StoppedException("目标浏览器窗口无效或已关闭。");
        }

        string? resultUrl = null;
        Exception? capturedException = null;

        var thread = new Thread(() =>
        {
            try
            {
                resultUrl = ExtractUrlViaUia(hwnd);
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.MTA);
        thread.IsBackground = true;
        thread.Start();

        bool completed = thread.Join(TimeSpan.FromSeconds(5));
        if (!completed)
        {
            throw new StoppedException("读取浏览器地址栏超时（5秒），请重试。");
        }

        if (capturedException != null)
        {
            if (capturedException is StoppedException)
            {
                throw capturedException;
            }

            throw new StoppedException($"读取浏览器地址栏失败：{capturedException.Message}", capturedException);
        }

        return resultUrl ?? throw new StoppedException("未能获取到有效的浏览器网址。");
    }

    private static string ExtractUrlViaUia(nint hwnd)
    {
        AutomationElement windowElement;
        try
        {
            windowElement = AutomationElement.FromHandle(hwnd);
        }
        catch (Exception ex)
        {
            throw new StoppedException($"无法获取浏览器窗口的自动化元素：{ex.Message}", ex);
        }

        if (windowElement == null)
        {
            throw new StoppedException("无法连接到浏览器窗口的 UI Automation。");
        }

        // 查找所有 Edit 类型的控件
        var editCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
        AutomationElementCollection edits = windowElement.FindAll(TreeScope.Descendants, editCondition);

        AutomationElement? addressBarElement = null;


        foreach (AutomationElement edit in edits)
        {
            try
            {
                string name = edit.Current.Name ?? string.Empty;
                string autoId = edit.Current.AutomationId ?? string.Empty;

                // 优先根据名称或 ID 匹配地址栏特征
                if (name.Contains("地址", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Address", StringComparison.OrdinalIgnoreCase) ||
                    autoId.Contains("address", StringComparison.OrdinalIgnoreCase))
                {
                    // A web-page input containing a URL must never impersonate the address bar.
                    var parent = TreeWalker.RawViewWalker.GetParent(edit);
                    bool webContent = false;
                    bool insideWindow = false;
                    for (int depth = 0; parent is not null && depth < 100; depth++)
                    {
                        webContent |= parent.Current.ControlType == ControlType.Document;
                        if (Automation.Compare(parent, windowElement)) { insideWindow = true; break; }
                        parent = TreeWalker.RawViewWalker.GetParent(parent);
                    }
                    if (!webContent && insideWindow) { addressBarElement = edit; break; }
                }

                // 次选：检测是否已有 ValuePattern 且其内容符合 URL 特征
            }
            catch (ElementNotAvailableException)
            {
                // 控件在遍历过程中可能被浏览器刷新重构，安全忽略
            }
            catch (COMException)
            {
                // COM 异常安全忽略，继续检查其他候选元素
            }
        }

        string rawUrl = string.Empty;

        if (addressBarElement != null)
        {
            try
            {
                if (addressBarElement.TryGetCurrentPattern(ValuePattern.Pattern, out var patternObj) &&
                    patternObj is ValuePattern vp)
                {
                    rawUrl = vp.Current.Value ?? string.Empty;
                }
                else
                {
                    rawUrl = addressBarElement.Current.Name ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                throw new StoppedException($"读取地址栏内容失败：{ex.Message}", ex);
            }
        }

        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            throw new StoppedException("无法读取浏览器地址栏中的网址。请确认已打开 PTA 页面。");
        }

        rawUrl = rawUrl.Trim();

        // 统一协议前缀：若缺失 http/https，补齐为 https://
        if (!rawUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !rawUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            rawUrl = "https://" + rawUrl;
        }

        return rawUrl;
    }
}
