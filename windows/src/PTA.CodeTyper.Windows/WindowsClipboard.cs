// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using PTA.CodeTyper.Core;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace PTA.CodeTyper.Windows;

/// <summary>
/// 基于 Win32 原生 API 的线程安全剪贴板读取与写入器。
/// 支持跨 STA/MTA 线程调度，并包含重试机制以应对外部剪贴板锁争用。
/// </summary>
public static class WindowsClipboard
{
    private const int MaxOpenRetries = 10;
    private const int RetryDelayMs = 15;

    /// <summary>
    /// 从系统剪贴板获取纯文本。若无文本或读取失败则返回空字符串。
    /// 自动确保在 STA 单元线程中执行。
    /// </summary>
    /// <returns>剪贴板纯文本内容。</returns>
    public static string GetText()
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return GetTextInternal();
        }

        return RunInSta(GetTextInternal);
    }

    /// <summary>
    /// 向系统剪贴板写入纯文本。
    /// 自动确保在 STA 单元线程中执行。
    /// </summary>
    /// <param name="text">要写入剪贴板的纯文本。</param>
    public static void SetText(string text)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            SetTextInternal(text);
        }
        else
        {
            RunInSta(() => SetTextInternal(text));
        }
    }

    /// <summary>
    /// 获取当前剪贴板序列号，用于检测外部程序对剪贴板内容的更改。
    /// </summary>
    /// <returns>剪贴板序列号。</returns>
    public static uint GetSequenceNumber() => NativeMethods.GetClipboardSequenceNumber();

    private static string GetTextInternal()
    {
        if (!TryOpenClipboard())
        {
            throw new StoppedException("Clipboard is busy; input has been stopped.");
        }

        try
        {
            nint hData = NativeMethods.GetClipboardData(NativeMethods.CF_UNICODETEXT);
            if (hData == nint.Zero)
            {
                return string.Empty;
            }

            nint pData = NativeMethods.GlobalLock(hData);
            if (pData == nint.Zero)
            {
                return string.Empty;
            }

            try
            {
                return Marshal.PtrToStringUni(pData) ?? string.Empty;
            }
            finally
            {
                NativeMethods.GlobalUnlock(hData);
            }
        }
        finally
        {
            NativeMethods.CloseClipboard();
        }
    }

    private static void SetTextInternal(string? text)
    {
        // An OLE owner window is needed; OpenClipboard(NULL) loses ownership on EmptyClipboard.
        for (int attempt = 0; attempt < MaxOpenRetries; attempt++)
        {
            try
            {
                System.Windows.Clipboard.SetDataObject(text ?? string.Empty, copy: true);
                return;
            }
            catch (ExternalException) when (attempt < MaxOpenRetries - 1)
            {
                Thread.Sleep(RetryDelayMs);
            }
        }
    }

    private static bool TryOpenClipboard()
    {
        for (int i = 0; i < MaxOpenRetries; i++)
        {
            if (NativeMethods.OpenClipboard(nint.Zero))
            {
                return true;
            }

            Thread.Sleep(RetryDelayMs);
        }

        return false;
    }

    private static void RunInSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();

        if (error != null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    private static T RunInSta<T>(Func<T> func)
    {
        T result = default!;
        RunInSta(() => { result = func(); });
        return result;
    }
}
