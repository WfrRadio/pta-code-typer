// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

namespace PTA.CodeTyper.Core;

/// <summary>
/// 平台特定键盘与浏览器交互后端的统一接口。
/// </summary>
public interface IInputBackend : IDisposable
{
    /// <summary>
    /// 获取当前操作的取消控制对象。
    /// </summary>
    CancellationControl Control { get; }

    /// <summary>
    /// 获取已锁定的浏览器显示名称（如 "Microsoft Edge"、"Google Chrome"）。
    /// </summary>
    string BrowserName { get; }

    /// <summary>
    /// 获取已锁定的目标页面 URL。
    /// </summary>
    string TargetUrl { get; }

    /// <summary>
    /// 检查取消状态及环境约束（如前台窗口焦点、热键等）。
    /// </summary>
    void Check();

    /// <summary>
    /// 锁定目标浏览器及其代码编辑区域。
    /// </summary>
    /// <param name="browser">选定的浏览器标识或 "auto"。</param>
    Task LockTargetAsync(string browser);

    /// <summary>
    /// 检查当前代码框是否为空。
    /// </summary>
    /// <returns>若代码框内容为空则返回 true，否则返回 false。</returns>
    bool IsEditorEmpty();

    /// <summary>
    /// 读取当前代码框中的完整文本。
    /// </summary>
    /// <returns>代码框文本。</returns>
    Task<string> ReadEditorAsync();

    // Keyboard operations

    /// <summary>
    /// 模拟键入单个文本字符。
    /// </summary>
    /// <param name="character">要输入的字符或 Unicode 标量。</param>
    Task TextAsync(string character);

    /// <summary>
    /// 模拟按下 Escape 键。
    /// </summary>
    Task EscapeAsync();

    /// <summary>
    /// 模拟全选快捷键 (Ctrl+A / Cmd+A)。
    /// </summary>
    Task SelectAllAsync();

    /// <summary>
    /// 模拟复制快捷键 (Ctrl+C / Cmd+C)。
    /// </summary>
    Task CopyAsync();

    /// <summary>
    /// 模拟按下 Backspace 键。
    /// </summary>
    Task BackspaceAsync();

    /// <summary>
    /// 模拟按下 Delete 键（向前删除）。
    /// </summary>
    Task DeleteForwardAsync();

    /// <summary>
    /// 模拟按下 Enter 键。
    /// </summary>
    Task EnterAsync();

    /// <summary>
    /// 移动光标至行首 (Home / Cmd+Left)。
    /// </summary>
    Task LineStartAsync();

    /// <summary>
    /// 移动光标至行尾 (End / Cmd+Right)。
    /// </summary>
    Task LineEndAsync();

    /// <summary>
    /// 模拟按下向右方向键。
    /// </summary>
    Task RightAsync();

    /// <summary>
    /// 清除编辑器在键入字符后可能自动补充的尾部匹配符号或多余内容。
    /// </summary>
    Task TrimTailAsync();

    /// <summary>
    /// 清除按下回车后编辑器自动生成的缩进空白。
    /// </summary>
    Task ClearAutoIndentAsync();

    // Clipboard

    /// <summary>
    /// 获取系统剪贴板中的纯文本。
    /// </summary>
    /// <returns>剪贴板纯文本内容。</returns>
    string ClipboardGet();

    /// <summary>
    /// 设置系统剪贴板中的纯文本。
    /// </summary>
    /// <param name="text">要写入剪贴板的纯文本。</param>
    void ClipboardSet(string text);
}
