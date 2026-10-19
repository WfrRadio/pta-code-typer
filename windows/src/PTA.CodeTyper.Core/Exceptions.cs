// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using System.Text;

namespace PTA.CodeTyper.Core;

/// <summary>
/// 终止输入的基类异常。焦点丢失、权限不足等错误不可重试。
/// </summary>
public class StoppedException : Exception
{
    public StoppedException(string message)
        : base(message)
    {
    }

    public StoppedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// 用户主动终止或热键取消输入的异常。
/// </summary>
public class CancelledException : StoppedException
{
    public CancelledException(string message = "已停止输入。编辑框中可能只有部分代码，请检查后再操作。")
        : base(message)
    {
    }

    public CancelledException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// 文本校验失败的异常，包含首次出现不一致的位置、行号、列号及片段。
/// </summary>
public class MismatchException : StoppedException
{
    /// <summary>
    /// 预期的代码文本。
    /// </summary>
    public string Expected { get; }

    /// <summary>
    /// 实际读取到的代码文本。
    /// </summary>
    public string Actual { get; }

    /// <summary>
    /// 首个不一致 Unicode scalar 的 0-based 索引。
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// 首个不一致字符所在的 1-based 行号。
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// 首个不一致字符所在的 1-based 列号。
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// 预期字符的片段表示。
    /// </summary>
    public string ExpectedSnippet { get; }

    /// <summary>
    /// 实际字符的片段表示。
    /// </summary>
    public string ActualSnippet { get; }

    public MismatchException(string expected, string actual)
        : this(expected, actual, CalculateMismatch(expected ?? string.Empty, actual ?? string.Empty))
    {
    }

    private MismatchException(
        string expected,
        string actual,
        (int position, int line, int column, string expectedSnippet, string actualSnippet) details)
        : base($"第 {details.line} 行第 {details.column} 列不一致：预期 {details.expectedSnippet}，实际 {details.actualSnippet}")
    {
        Expected = expected;
        Actual = actual;
        Position = details.position;
        Line = details.line;
        Column = details.column;
        ExpectedSnippet = details.expectedSnippet;
        ActualSnippet = details.actualSnippet;
    }

    private static (int position, int line, int column, string expectedSnippet, string actualSnippet) CalculateMismatch(
        string expected,
        string actual)
    {
        var expectedRunes = expected.EnumerateRunes().ToArray();
        var actualRunes = actual.EnumerateRunes().ToArray();

        int minLen = Math.Min(expectedRunes.Length, actualRunes.Length);
        int pos = 0;
        while (pos < minLen && expectedRunes[pos] == actualRunes[pos])
        {
            pos++;
        }

        int line = 1;
        int lastNewlinePos = -1;
        var newlineRune = new Rune('\n');
        for (int i = 0; i < pos; i++)
        {
            if (expectedRunes[i] == newlineRune)
            {
                line++;
                lastNewlinePos = i;
            }
        }

        int column = pos - lastNewlinePos;

        string expSnippet = FormatSnippet(expectedRunes, pos);
        string actSnippet = FormatSnippet(actualRunes, pos);

        return (pos, line, column, expSnippet, actSnippet);
    }

    private static string FormatSnippet(Rune[] runes, int index)
    {
        if (index >= runes.Length)
        {
            return "''";
        }

        var rune = runes[index];
        string s = rune.ToString();
        return s switch
        {
            "\n" => "'\\n'",
            "\r" => "'\\r'",
            "\t" => "'\\t'",
            "\'" => "'\\''",
            "\\" => "'\\\\'",
            _ => $"'{s}'"
        };
    }
}
