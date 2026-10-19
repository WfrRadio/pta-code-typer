// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using PTA.CodeTyper.Core;

namespace PTA.CodeTyper.Windows;

/// <summary>
/// 全局急停监视器：管理系统级全局热键（F8 优先，冲突时依次降级至 Ctrl+F8、Alt+F8 等），
/// 并结合后台轮询实时检测物理按键状态与屏幕左上角鼠标急停区。
/// </summary>
public sealed class StopMonitor : IDisposable
{
    private const int HotkeyId = 9001;

    private readonly CancellationControl _control;
    private nint _registeredHwnd;
    private int _registeredId;
    private CancellationTokenSource? _pollingCts;
    private Task? _pollingTask;
    private bool _disposed;

    /// <summary>
    /// 获取全局热键是否成功注册。
    /// </summary>
    public bool HotkeyRegistered { get; private set; }

    /// <summary>
    /// 获取当前生效的停止按键提示文本（如 "F8"、"Ctrl+F8"、"F8 (轮询检测)"）。
    /// </summary>
    public string StopKeyName { get; private set; } = "F8";

    public StopMonitor(CancellationControl control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
    }

    /// <summary>
    /// 尝试将 F8 注册为当前窗口的全局热键。
    /// 若 F8 被占用，依次尝试 Ctrl+F8、Alt+F8、Shift+F8。
    /// 若全部占用，则自动切换为纯轮询检测模式。
    /// 无论注册成功与否，均会启动辅助后台轮询以确保最高的急停响应度。
    /// </summary>
    /// <param name="hwnd">用于接收热键消息的窗口句柄。</param>
    public void RegisterStopKey(nint hwnd)
    {
        UnregisterCurrentHotkey();

        (uint Modifiers, string Label)[] candidates =
        [
            (NativeMethods.MOD_NOREPEAT, "F8"),
            (NativeMethods.MOD_CONTROL | NativeMethods.MOD_NOREPEAT, "Ctrl+F8"),
            (NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT, "Alt+F8"),
            (NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT, "Shift+F8")
        ];

        bool registered = false;

        if (hwnd != nint.Zero && NativeMethods.IsWindow(hwnd))
        {
            foreach (var (modifiers, label) in candidates)
            {
                if (NativeMethods.RegisterHotKey(hwnd, HotkeyId, modifiers, NativeMethods.VK_F8))
                {
                    registered = true;
                    _registeredHwnd = hwnd;
                    _registeredId = HotkeyId;
                    HotkeyRegistered = true;
                    StopKeyName = label;
                    break;
                }
            }
        }

        if (!registered)
        {
            HotkeyRegistered = false;
            StopKeyName = "F8 (轮询检测)";
        }

        StartBackgroundPolling();
    }

    /// <summary>
    /// 处理窗口消息循环中的 WM_HOTKEY 消息。
    /// </summary>
    /// <param name="msg">消息代码。</param>
    /// <param name="wParam">消息 wParam 参数（热键 ID）。</param>
    /// <param name="lParam">消息 lParam 参数。</param>
    /// <returns>若该消息已被处理并触发急停则返回 true，否则返回 false。</returns>
    public bool ProcessMessage(int msg, nint wParam, nint lParam)
    {
        if (msg == NativeMethods.WM_HOTKEY && (int)wParam == _registeredId)
        {
            _control.Cancel();
            return true;
        }

        return false;
    }

    private void StartBackgroundPolling()
    {
        StopBackgroundPolling();

        _pollingCts = new CancellationTokenSource();
        var token = _pollingCts.Token;

        _pollingTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested && !_control.IsCancellationRequested)
            {
                // 1. 物理检查 F8 是否按下
                if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_F8) & 0x8000) != 0)
                {
                    _control.Cancel();
                    break;
                }

                // 2. 检查鼠标左上角急停区
                if (IsMouseInEmergencyCorner())
                {
                    _control.Cancel();
                    break;
                }

                try
                {
                    await Task.Delay(40, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }, token);
    }

    private void StopBackgroundPolling()
    {
        if (_pollingCts != null)
        {
            try
            {
                _pollingCts.Cancel();
                _pollingCts.Dispose();
            }
            catch
            {
                // 忽略取消时的异常
            }
            finally
            {
                _pollingCts = null;
                _pollingTask = null;
            }
        }
    }

    private void UnregisterCurrentHotkey()
    {
        if (HotkeyRegistered && _registeredHwnd != nint.Zero)
        {
            try
            {
                NativeMethods.UnregisterHotKey(_registeredHwnd, _registeredId);
            }
            catch
            {
                // 忽略卸载热键失败
            }
            finally
            {
                HotkeyRegistered = false;
                _registeredHwnd = nint.Zero;
                _registeredId = 0;
            }
        }
    }

    private static bool IsMouseInEmergencyCorner()
    {
        if (NativeMethods.GetCursorPos(out var pt))
        {
            return pt.X >= 0 && pt.X <= 3 && pt.Y >= 0 && pt.Y <= 3;
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopBackgroundPolling();
        UnregisterCurrentHotkey();
    }
}
