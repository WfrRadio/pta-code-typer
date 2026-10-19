// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using System.Windows.Automation;
using PTA.CodeTyper.Core;

namespace PTA.CodeTyper.Windows;

/// <summary>
/// 目标浏览器锁定与键入期间的前台窗口、焦点及安全角持续守护器。
/// </summary>
public sealed class TargetGuard : IDisposable
{
    private int[]? _editorRuntimeId;
    /// <summary>
    /// 获取当前锁定的目标窗口句柄。
    /// </summary>
    public nint TargetHwnd { get; private set; }

    /// <summary>
    /// 获取当前锁定的目标进程 ID。
    /// </summary>
    public uint TargetPid { get; private set; }

    /// <summary>
    /// 获取当前锁定的浏览器显示名称。
    /// </summary>
    public string BrowserName { get; private set; } = string.Empty;

    /// <summary>
    /// 获取当前锁定的目标网页 URL。
    /// </summary>
    public string TargetUrl { get; private set; } = string.Empty;

    /// <summary>
    /// 锁定当前前台浏览器窗口目标，验证浏览器类型、PTA 网址及代码编辑框可编辑性。
    /// </summary>
    /// <param name="selectedBrowser">选定的浏览器标识或 "auto"。</param>
    /// <exception cref="StoppedException">当目标不符合要求或无法锁定时抛出。</exception>
    public void LockTarget(string selectedBrowser)
    {
        var (hwnd, pid, _, browserLabel) = BrowserDetector.GetForegroundBrowser(selectedBrowser);
        string url = BrowserDetector.GetBrowserUrl(hwnd);

        if (!TextNormalizer.IsValidPtaUrl(url))
        {
            throw new StoppedException("当前标签页不是 pintia.cn，未执行清空或输入。");
        }

        TargetHwnd = hwnd;
        TargetPid = pid;
        BrowserName = browserLabel;
        TargetUrl = url;

        _editorRuntimeId = ValidateFocusedEditor(hwnd);
    }

    /// <summary>
    /// 持续检查当前输入环境约束：
    /// 1. 取消控制标记是否已触发。
    /// 2. 是否检测到紧急停止键（F8）。
    /// 3. 前台窗口句柄与进程 ID 是否仍为已锁定的目标。
    /// 4. 鼠标是否移入主显示器左上角安全急停区（3像素内）。
    /// </summary>
    /// <param name="control">取消控制实例。</param>
    /// <exception cref="CancelledException">用户主动触发取消、按下 F8 或鼠标移至安全角时抛出。</exception>
    /// <exception cref="StoppedException">前台窗口或进程发生切换时抛出。</exception>
    public void Check(CancellationControl control)
    {
        ArgumentNullException.ThrowIfNull(control);

        // 1. 检查取消信号
        control.Check();

        // 2. 检查全局 F8 按键状态
        if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_F8) & 0x8000) != 0)
        {
            throw new CancelledException("检测到 F8，已停止输入。");
        }

        // 3. 检查前台目标窗口与进程
        if (TargetHwnd != nint.Zero)
        {
            nint foregroundHwnd = NativeMethods.GetForegroundWindow();
            if (foregroundHwnd != TargetHwnd)
            {
                throw new StoppedException("前台应用发生切换，已停止输入。");
            }

            NativeMethods.GetWindowThreadProcessId(foregroundHwnd, out uint currentPid);
            if (currentPid != TargetPid)
            {
                throw new StoppedException("前台进程发生切换，已停止输入。");
            }
        }

        if (_editorRuntimeId is not null)
        {
            try
            {
                var focused = AutomationElement.FocusedElement;
                if (focused is null || !focused.GetRuntimeId().SequenceEqual(_editorRuntimeId))
                    throw new StoppedException("Editor focus changed; input stopped.");
            }
            catch (StoppedException) { throw; }
            catch (Exception ex) { throw new StoppedException("Cannot confirm editor focus; input stopped.", ex); }
        }

        // 4. 检查鼠标左上角急停区
        if (IsMouseInEmergencyCorner())
        {
            throw new CancelledException("鼠标已移至屏幕左上角，输入已停止。");
        }
    }

    /// <summary>
    /// 检查鼠标当前物理坐标是否处于主显示器左上角（(0,0) 附近 3 像素以内）。
    /// </summary>
    private static bool IsMouseInEmergencyCorner()
    {
        if (NativeMethods.GetCursorPos(out var pt))
        {
            return pt.X >= 0 && pt.X <= 3 && pt.Y >= 0 && pt.Y <= 3;
        }

        return false;
    }

    /// <summary>
    /// 辅助校验当前获得焦点的控件是否为可编辑代码区域。
    /// </summary>
    private static int[] ValidateFocusedEditor(nint expectedWindow)
    {
        Exception? validationError = null;
        int[]? runtimeId = null;
        var thread = new Thread(() =>
        {
            try
            {
                var focused = AutomationElement.FocusedElement
                    ?? throw new StoppedException("Cannot read editor focus.");
                var type = focused.Current.ControlType;
                if (type != ControlType.Edit || !focused.Current.IsEnabled || focused.Current.IsPassword)
                    throw new StoppedException("Click the editable PTA code area before starting.");
                if (focused.TryGetCurrentPattern(ValuePattern.Pattern, out var value) &&
                    value is ValuePattern vp && vp.Current.IsReadOnly)
                    throw new StoppedException("The focused text area is read-only.");

                // Require a web document ancestor inside the exact target browser window.
                var root = AutomationElement.FromHandle(expectedWindow);
                var walker = TreeWalker.RawViewWalker;
                var parent = walker.GetParent(focused);
                bool inDocument = false;
                bool inWindow = false;
                for (int depth = 0; parent is not null && depth < 100; depth++)
                {
                    inDocument |= parent.Current.ControlType == ControlType.Document;
                    if (Automation.Compare(parent, root)) { inWindow = true; break; }
                    parent = walker.GetParent(parent);
                }
                if (!inDocument || !inWindow)
                    throw new StoppedException("Focus is outside the browser page code area.");
                runtimeId = focused.GetRuntimeId();
            }
            catch (Exception ex) { validationError = ex; }
        });
        thread.SetApartmentState(ApartmentState.MTA);
        thread.IsBackground = true;
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(2)))
            throw new StoppedException("Editor focus validation timed out; no input was started.");
        if (validationError is not null)
            throw new StoppedException("Cannot validate editable code area: " + validationError.Message, validationError);
        return runtimeId ?? throw new StoppedException("Cannot identify the code editor.");
    }

    public void Dispose()
    {
        _editorRuntimeId = null;
        TargetHwnd = nint.Zero;
        TargetPid = 0;
        BrowserName = string.Empty;
        TargetUrl = string.Empty;
    }
}
