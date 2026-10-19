// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using PTA.CodeTyper.Core;

namespace PTA.CodeTyper.Windows;

/// <summary>
/// 基于 Win32 SendInput API 的键盘与 Unicode 文本模拟发送器。
/// </summary>
public sealed class InputSender
{
    /// <summary>
    /// 通过 KEYEVENTF_UNICODE 发送单个 Unicode 码元（UTF-16 code unit）。
    /// </summary>
    /// <param name="ch">待发送的 UTF-16 字符。</param>
    public static void SendUnicodeChar(char ch)
    {
        NativeMethods.INPUT[] inputs =
        [
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = ch,
                    dwFlags = NativeMethods.KEYEVENTF_UNICODE,
                    time = 0,
                    dwExtraInfo = 0
                }
            },
            new NativeMethods.INPUT
            {
                type = NativeMethods.INPUT_KEYBOARD,
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = 0,
                    wScan = ch,
                    dwFlags = NativeMethods.KEYEVENTF_UNICODE | NativeMethods.KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = 0
                }
            }
        ];

        SendInputs(inputs);
    }

    /// <summary>
    /// 逐 Rune（Unicode 标量值）发送完整文本。若遇到代理对，按顺序发送高位和低位码元。
    /// </summary>
    /// <param name="text">待发送的文本字符串。</param>
    public static void SendUnicodeText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.IsBmp)
            {
                SendUnicodeChar((char)rune.Value);
            }
            else
            {
                string s = rune.ToString();
                SendUnicodeChar(s[0]);
                SendUnicodeChar(s[1]);
            }
        }
    }

    /// <summary>
    /// 按下并释放指定虚拟键，支持按顺序压入与释放 Ctrl/Shift 修饰键及扩展键标志。
    /// 按键顺序：Ctrl down -> Shift down -> Key down -> Key up -> Shift up -> Ctrl up。
    /// </summary>
    /// <param name="vk">虚拟键代码。</param>
    /// <param name="ctrl">是否附带 Ctrl 修饰键。</param>
    /// <param name="shift">是否附带 Shift 修饰键。</param>
    /// <param name="extended">是否为扩展按键（如方向键、Home/End、Delete）。</param>
    public static void SendVirtualKey(ushort vk, bool ctrl = false, bool shift = false, bool extended = false)
    {
        var inputs = new List<NativeMethods.INPUT>(6);
        uint extFlag = extended ? NativeMethods.KEYEVENTF_EXTENDEDKEY : 0;

        // 1. 修饰键按下：Ctrl 先于 Shift
        if (ctrl)
        {
            inputs.Add(CreateKeyInput(NativeMethods.VK_CONTROL, 0));
        }
        if (shift)
        {
            inputs.Add(CreateKeyInput(NativeMethods.VK_SHIFT, 0));
        }

        // 2. 目标键按下与抬起
        inputs.Add(CreateKeyInput(vk, extFlag));
        inputs.Add(CreateKeyInput(vk, extFlag | NativeMethods.KEYEVENTF_KEYUP));

        // 3. 修饰键释放：Shift 先于 Ctrl
        if (shift)
        {
            inputs.Add(CreateKeyInput(NativeMethods.VK_SHIFT, NativeMethods.KEYEVENTF_KEYUP));
        }
        if (ctrl)
        {
            inputs.Add(CreateKeyInput(NativeMethods.VK_CONTROL, NativeMethods.KEYEVENTF_KEYUP));
        }

        SendInputs(inputs.ToArray());
    }

    /// <summary>
    /// 检查是否有任何修饰键（Ctrl/Shift/Alt/Win）处于物理按下状态。
    /// 若有修饰键按下，则抛出 <see cref="StoppedException"/>。
    /// </summary>
    public static void EnsureModifiersReleased()
    {
        int[] modifierKeys =
        [
            NativeMethods.VK_CONTROL,
            NativeMethods.VK_SHIFT,
            NativeMethods.VK_MENU,
            NativeMethods.VK_LWIN,
            NativeMethods.VK_RWIN
        ];

        foreach (int vk in modifierKeys)
        {
            if ((NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0)
            {
                throw new StoppedException("检测到修饰键（Ctrl/Shift/Alt/Win）处于按下状态，请松开按键后重试。");
            }
        }
    }

    /// <summary>
    /// 强制释放所有可能处于按下状态的修饰键（Ctrl、Shift、Alt、Win），防止按键残留。
    /// </summary>
    public static void ReleaseModifiers()
    {
        ushort[] modifierKeys =
        [
            NativeMethods.VK_CONTROL,
            NativeMethods.VK_SHIFT,
            NativeMethods.VK_MENU,
            NativeMethods.VK_LWIN,
            NativeMethods.VK_RWIN
        ];

        var inputs = new List<NativeMethods.INPUT>(modifierKeys.Length);
        foreach (ushort vk in modifierKeys)
        {
            inputs.Add(CreateKeyInput(vk, NativeMethods.KEYEVENTF_KEYUP));
        }

        SendInputs(inputs.ToArray());
    }

    private static NativeMethods.INPUT CreateKeyInput(ushort vk, uint flags)
    {
        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            ki = new NativeMethods.KEYBDINPUT
            {
                wVk = vk,
                wScan = 0,
                dwFlags = flags,
                time = 0,
                dwExtraInfo = 0
            }
        };
    }

    private static void SendInputs(NativeMethods.INPUT[] inputs)
    {
        if (inputs == null || inputs.Length == 0)
        {
            return;
        }

        int cbSize = Marshal.SizeOf<NativeMethods.INPUT>();
        uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, cbSize);
        if (sent != inputs.Length)
        {
            int error = Marshal.GetLastWin32Error();
            throw new Win32Exception(error, $"SendInput 执行失败：已发送 {sent}/{inputs.Length} 个输入事件，错误代码：{error}。");
        }
    }
}
