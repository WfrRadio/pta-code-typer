// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using System.Text;
using PTA.CodeTyper.Core;

namespace PTA.CodeTyper.Windows;

/// <summary>
/// 针对 Windows 平台的完整 <see cref="IInputBackend"/> 实现。
/// 包含目标锁定、状态检查、按键模拟、修饰键管理及剪贴板安全读写。
/// </summary>
public sealed class WindowsInputBackend : IInputBackend
{
    private const double DefaultKeyDelaySeconds = 0.004;

    private readonly CancellationControl _control;
    private readonly TargetGuard _guard = new();

    /// <inheritdoc/>
    public CancellationControl Control => _control;

    /// <inheritdoc/>
    public string BrowserName => _guard.BrowserName;

    /// <inheritdoc/>
    public string TargetUrl => _guard.TargetUrl;

    public WindowsInputBackend(CancellationControl control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
    }

    /// <inheritdoc/>
    public void Check() => _guard.Check(_control);

    /// <inheritdoc/>
    public Task LockTargetAsync(string browser)
    {
        _guard.LockTarget(browser);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public bool IsEditorEmpty()
    {
        Check();
        string original = ClipboardGet();
        string marker = "PTA_EMPTY_CHECK_" + Guid.NewGuid().ToString("N");

        try
        {
            ClipboardSet(marker);
            Check();
            InputSender.SendVirtualKey(NativeMethods.VK_A, ctrl: true);
            Thread.Sleep(50);
            Check();
            InputSender.SendVirtualKey(NativeMethods.VK_C, ctrl: true);
            Thread.Sleep(80);

            string current = ClipboardGet();

            // 移动向右方向键以取消选中状态，避免残留全选
            InputSender.SendVirtualKey(NativeMethods.VK_RIGHT);

            // 若剪贴板文本仍为探测标记或为空，表明编辑框中无内容
            // An unchanged marker means Copy did not respond, not an empty editor.
            return current != marker && string.IsNullOrEmpty(current);
        }
        catch
        {
            return false;
        }
        finally
        {
            try
            {
                ClipboardSet(original);
            }
            catch
            {
                // 忽略恢复原剪贴板内容时的异常
            }
        }
    }

    /// <inheritdoc/>
    public async Task<string> ReadEditorAsync()
    {
        return await ClipboardReader.ReadEditorAsync(this).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task TextAsync(string character)
    {
        Check();
        if (string.IsNullOrEmpty(character))
        {
            return;
        }

        foreach (var rune in character.EnumerateRunes())
        {
            if (rune.IsBmp)
            {
                InputSender.SendUnicodeChar((char)rune.Value);
            }
            else
            {
                string s = rune.ToString();
                InputSender.SendUnicodeChar(s[0]);
                InputSender.SendUnicodeChar(s[1]);
            }
        }

        await _control.WaitAsync(DefaultKeyDelaySeconds).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task EscapeAsync()
    {
        Check();
        InputSender.SendVirtualKey(NativeMethods.VK_ESCAPE);
        await _control.WaitAsync(DefaultKeyDelaySeconds).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SelectAllAsync()
    {
        Check();
        InputSender.SendVirtualKey(NativeMethods.VK_A, ctrl: true);
        await _control.WaitAsync(DefaultKeyDelaySeconds).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task CopyAsync()
    {
        Check();
        InputSender.SendVirtualKey(NativeMethods.VK_C, ctrl: true);
        await _control.WaitAsync(DefaultKeyDelaySeconds).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task BackspaceAsync()
    {
        Check();
        InputSender.SendVirtualKey(NativeMethods.VK_BACK);
        await _control.WaitAsync(DefaultKeyDelaySeconds).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteForwardAsync()
    {
        Check();
        InputSender.SendVirtualKey(NativeMethods.VK_DELETE, extended: true);
        await _control.WaitAsync(DefaultKeyDelaySeconds).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task EnterAsync()
    {
        Check();
        InputSender.SendVirtualKey(NativeMethods.VK_RETURN);
        await _control.WaitAsync(DefaultKeyDelaySeconds).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task LineStartAsync()
    {
        Check();
        InputSender.SendVirtualKey(NativeMethods.VK_HOME);
        await _control.WaitAsync(DefaultKeyDelaySeconds).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task LineEndAsync()
    {
        Check();
        InputSender.SendVirtualKey(NativeMethods.VK_END);
        await _control.WaitAsync(DefaultKeyDelaySeconds).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RightAsync()
    {
        Check();
        InputSender.SendVirtualKey(NativeMethods.VK_RIGHT);
        await _control.WaitAsync(DefaultKeyDelaySeconds).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task TrimTailAsync()
    {
        await EscapeAsync().ConfigureAwait(false);
        Check();
        InputSender.SendVirtualKey(NativeMethods.VK_END, ctrl: true, shift: true, extended: true);
        await _control.WaitAsync(DefaultKeyDelaySeconds).ConfigureAwait(false);
        await DeleteForwardAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ClearAutoIndentAsync()
    {
        await TrimTailAsync().ConfigureAwait(false);
        Check();
        InputSender.SendVirtualKey(NativeMethods.VK_HOME, shift: true);
        await _control.WaitAsync(DefaultKeyDelaySeconds).ConfigureAwait(false);
        await DeleteForwardAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public string ClipboardGet() => WindowsClipboard.GetText();

    /// <inheritdoc/>
    public void ClipboardSet(string text) => WindowsClipboard.SetText(text);

    public void Dispose()
    {
        _guard.Dispose();
    }
}
