// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

namespace PTA.CodeTyper.Core;

/// <summary>
/// 剪贴板读取辅助类，确保读取稳定性并恢复原剪贴板内容，绝不使用粘贴进行输入。
/// </summary>
public static class ClipboardReader
{
    /// <summary>
    /// 从编辑框中安全读取文本：
    /// 保存原剪贴板内容，写入唯一探测标记，全选并复制，轮询等待连续两次读取到相同且非标记的内容，
    /// 右移取消选中以保护光标位置，恢复原剪贴板内容，并返回换行符统一为 LF 的文本。
    /// 最多外层重试 3 次，每次包含 5 次稳定性检查。
    /// </summary>
    /// <param name="backend">输入后端对象。</param>
    /// <returns>标准化换行后的代码框文本。</returns>
    /// <exception cref="ArgumentNullException">当 backend 为 null 时抛出。</exception>
    /// <exception cref="StoppedException">当复制未响应或剪贴板不稳定时抛出。</exception>
    public static async Task<string> ReadEditorAsync(IInputBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);

        string original = backend.ClipboardGet();
        string marker = "PTA_CHECK_" + Guid.NewGuid().ToString("N");

        try
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                backend.ClipboardSet(marker);
                backend.Check();
                await backend.SelectAllAsync().ConfigureAwait(false);
                await backend.CopyAsync().ConfigureAwait(false);

                string? previous = null;
                for (int check = 0; check < 5; check++)
                {
                    await backend.Control.WaitAsync(0.1).ConfigureAwait(false);
                    string actual = backend.ClipboardGet();
                    if (actual != marker && actual == previous)
                    {
                        backend.Check();
                        await backend.RightAsync().ConfigureAwait(false);
                        return actual.Replace("\r\n", "\n").Replace('\r', '\n');
                    }
                    previous = actual;
                }
            }

            throw new StoppedException("复制未响应或剪贴板不稳定，无法校验。请暂停其他复制操作后重试。");
        }
        finally
        {
            backend.ClipboardSet(original);
        }
    }
}
