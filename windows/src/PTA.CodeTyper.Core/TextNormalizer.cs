// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using System.Text;
using System.Text.RegularExpressions;

namespace PTA.CodeTyper.Core;

/// <summary>
/// 代码规范化、PTA 平台 URL 校验与题型规则检查辅助类。
/// </summary>
public static class TextNormalizer
{
    private static readonly Regex MainFunctionRegex = new(
        @"^\s*(?:int|void|auto)\s+main\s*\(",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// 规范化待输入的代码：
    /// 1. 移除开头的 BOM (U+FEFF)。
    /// 2. 统一行尾为 LF (\n)。
    /// 3. 仅对行首缩进（空格、Tab、NBSP、窄 NBSP）进行替换和 Tab 展开（4 列制表位），不触及后续正文内容。
    /// 4. 拒绝包含控制字符（除 \n、\t 以外的 ord &lt; 32，以及 U+007F DEL）。
    /// 5. 拒绝包含无效 Unicode 字符（孤立代理对）。
    /// </summary>
    /// <param name="text">原始代码文本。</param>
    /// <returns>规范化后的代码文本。</returns>
    /// <exception cref="ArgumentNullException">当 text 为 null 时抛出。</exception>
    /// <exception cref="ArgumentException">当代码含控制字符或无效 Unicode 字符时抛出。</exception>
    public static string NormalizeCode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.StartsWith('\ufeff'))
        {
            text = text[1..];
        }

        text = text.Replace("\r\n", "\n").Replace('\r', '\n');

        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            int n = 0;
            while (n < line.Length && IsLeadingIndentChar(line[n]))
            {
                n++;
            }

            if (n > 0)
            {
                string indent = line[..n].Replace('\u00a0', ' ').Replace('\u202f', ' ');
                string expandedIndent = ExpandTabs(indent, 4);
                lines[i] = expandedIndent + line[n..];
            }
        }

        string result = string.Join("\n", lines);

        // 校验控制字符：ord < 32 (除 \n 和 \t) 以及 U+007F (DEL)
        for (int i = 0; i < result.Length; i++)
        {
            char ch = result[i];
            if ((ch < 32 && ch != '\n' && ch != '\t') || ch == 127)
            {
                throw new ArgumentException("代码含控制字符；请移除 NUL、Escape 等不可输入字符。", nameof(text));
            }
        }

        // 校验无效 Unicode 字符（孤立代理对）
        for (int i = 0; i < result.Length;)
        {
            if (!Rune.TryGetRuneAt(result, i, out Rune rune))
            {
                throw new ArgumentException("代码含无效 Unicode 字符，请重新以 UTF-8 导入。", nameof(text));
            }
            i += rune.Utf16SequenceLength;
        }

        return result;
    }

    private static bool IsLeadingIndentChar(char c) =>
        c == ' ' || c == '\t' || c == '\u00a0' || c == '\u202f';

    private static string ExpandTabs(string s, int tabSize = 4)
    {
        var sb = new StringBuilder(s.Length);
        int col = 0;
        foreach (char ch in s)
        {
            if (ch == '\t')
            {
                int spacesToAdd = tabSize - (col % tabSize);
                sb.Append(' ', spacesToAdd);
                col += spacesToAdd;
            }
            else
            {
                sb.Append(ch);
                col++;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// 校验 URL 是否属于 PTA (pintia.cn) 平台。
    /// 协议必须为 http 或 https，域名必须为 pintia.cn 或以 .pintia.cn 结尾。
    /// </summary>
    /// <param name="url">待校验的 URL 字符串。</param>
    /// <returns>若合法则返回 true，否则返回 false。</returns>
    public static bool IsValidPtaUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        string host = uri.Host.ToLowerInvariant();
        return host == "pintia.cn" || host.EndsWith(".pintia.cn", StringComparison.Ordinal);
    }

    /// <summary>
    /// 检测是否为函数题中误输入完整 main() 程序，并给出警告提示。
    /// </summary>
    /// <param name="url">当前题目页面的 URL。</param>
    /// <param name="code">待输入的代码文本。</param>
    /// <returns>若检测到风险则返回警告提示文本，否则返回空字符串。</returns>
    public static string GetTaskWarning(string? url, string? code)
    {
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(code))
        {
            return string.Empty;
        }

        string path = string.Empty;
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            path = uri.AbsolutePath;
        }
        else
        {
            path = url;
        }

        if (path.Contains("/problems/type/6", StringComparison.Ordinal) &&
            MainFunctionRegex.IsMatch(code))
        {
            return "当前是函数题，平台通常已提供 main()。完整程序应输入对应的编程题；输入校验通过不代表当前题目可以编译。";
        }

        return string.Empty;
    }
}
