// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text;

namespace PTA.CodeTyper.Core;

/// <summary>
/// 键入运行结果信息。
/// </summary>
public sealed record TypingResult
{
    /// <summary>
    /// 输入的代码总字符数。
    /// </summary>
    public int Characters { get; init; }

    /// <summary>
    /// 输入的代码总行数。
    /// </summary>
    public int Lines { get; init; }

    /// <summary>
    /// 报告所在目录路径。
    /// </summary>
    public string ReportPath { get; init; } = string.Empty;

    /// <summary>
    /// 报告所在目录路径别名。
    /// </summary>
    public string Report => ReportPath;

    /// <summary>
    /// 题目警告提示（若有）。
    /// </summary>
    public string Warning { get; init; } = string.Empty;

    /// <summary>
    /// 转换为字典格式。
    /// </summary>
    public Dictionary<string, object?> ToDictionary() => new()
    {
        ["characters"] = Characters,
        ["lines"] = Lines,
        ["report"] = ReportPath,
        ["warning"] = Warning
    };
}

/// <summary>
/// 核心键入编排引擎，实现逐字符输入、防自动缩进守卫、定期双重读取校验及降速重试机制。
/// </summary>
public static class TypingEngine
{
    /// <summary>
    /// 执行单次完整代码键入与校验。
    /// </summary>
    /// <param name="backend">输入后端。</param>
    /// <param name="code">已规范化的代码文本。</param>
    /// <param name="options">参数选项。</param>
    /// <param name="delay">当前尝试的字符输入延迟（秒）。</param>
    /// <param name="emit">事件回调代理。</param>
    /// <returns>最终从编辑器读取到的文本。</returns>
    /// <exception cref="MismatchException">当校验失败且重读后仍不匹配时抛出。</exception>
    public static async Task<string> TypeOnceAsync(
        IInputBackend backend,
        string code,
        Options options,
        double delay,
        Action<string, object?> emit)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(emit);

        backend.Check();
        await backend.EscapeAsync().ConfigureAwait(false);
        await backend.SelectAllAsync().ConfigureAwait(false);
        await backend.BackspaceAsync().ConfigureAwait(false);
        await backend.Control.WaitAsync(0.25).ConfigureAwait(false);

        string[] lines = code.Split('\n');
        string prefix = string.Empty;
        string actual = string.Empty;

        for (int index = 0; index < lines.Length; index++)
        {
            backend.Check();
            string line = lines[index];
            int indentSize = line.Length - line.TrimStart(' ').Length;
            string indent = line[..indentSize];
            string content = line[indentSize..];

            // 临时非空白字符守卫，防止编辑器将 '}' 触发自动缩进重排。
            await backend.TextAsync("x").ConfigureAwait(false);
            await backend.Control.WaitAsync(delay).ConfigureAwait(false);
            await backend.TrimTailAsync().ConfigureAwait(false);

            foreach (var rune in content.EnumerateRunes())
            {
                backend.Check();
                await backend.TextAsync(rune.ToString()).ConfigureAwait(false);
                await backend.Control.WaitAsync(delay).ConfigureAwait(false);
                await backend.TrimTailAsync().ConfigureAwait(false);
            }

            await backend.LineStartAsync().ConfigureAwait(false);
            await backend.DeleteForwardAsync().ConfigureAwait(false);

            foreach (char ch in indent)
            {
                await backend.TextAsync(ch.ToString()).ConfigureAwait(false);
                await backend.Control.WaitAsync(delay).ConfigureAwait(false);
            }

            await backend.LineEndAsync().ConfigureAwait(false);
            prefix += (index > 0 ? "\n" : "") + line;

            if ((index + 1) % options.CheckEvery == 0 || index == lines.Length - 1)
            {
                actual = await backend.ReadEditorAsync().ConfigureAwait(false);
                if (actual != prefix)
                {
                    actual = await backend.ReadEditorAsync().ConfigureAwait(false);
                }
                if (actual != prefix)
                {
                    throw new MismatchException(prefix, actual);
                }
            }

            emit("progress", new Dictionary<string, object?>
            {
                ["line"] = index + 1,
                ["total"] = lines.Length
            });

            if (index < lines.Length - 1)
            {
                await backend.EnterAsync().ConfigureAwait(false);
                await backend.Control.WaitAsync(Math.Max(delay, 0.08)).ConfigureAwait(false);
                await backend.ClearAutoIndentAsync().ConfigureAwait(false);
            }
        }

        return actual;
    }

    /// <summary>
    /// 执行完整的键入流程（参数校验、规范化、倒计时、锁定目标、安全备份、带降速重试的循环键入）。
    /// </summary>
    /// <param name="backend">输入后端。</param>
    /// <param name="source">原始代码字符串。</param>
    /// <param name="options">参数选项。</param>
    /// <param name="directory">报告存放目录。</param>
    /// <param name="emit">可选的事件回调代理。</param>
    /// <param name="countdown">是否执行倒计时。</param>
    /// <returns>键入结果信息。</returns>
    public static async Task<TypingResult> RunTypingAsync(
        IInputBackend backend,
        string source,
        Options options,
        string directory,
        Action<string, object?>? emit = null,
        bool countdown = true)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(directory);

        Action<string, object?> emitAction = emit ?? ((_, _) => { });

        options.Validate();
        string code = TextNormalizer.NormalizeCode(source);
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("请先读取剪贴板或导入非空代码文件。", nameof(source));
        }

        if (countdown)
        {
            var stopwatch = Stopwatch.StartNew();
            double totalSeconds = options.Countdown;
            while (true)
            {
                double remaining = totalSeconds - stopwatch.Elapsed.TotalSeconds;
                if (remaining <= 0)
                {
                    break;
                }
                emitAction("countdown", (int)remaining + 1);
                await backend.Control.WaitAsync(Math.Min(1.0, remaining)).ConfigureAwait(false);
            }
        }

        await backend.LockTargetAsync(options.Browser).ConfigureAwait(false);
        emitAction("target", new Dictionary<string, object?>
        {
            ["browser"] = backend.BrowserName,
            ["url"] = backend.TargetUrl
        });

        string warning = TextNormalizer.GetTaskWarning(backend.TargetUrl, code);
        if (!string.IsNullOrEmpty(warning))
        {
            emitAction("warning", warning);
        }

        // 首个破坏性按键前先备份现有内容。空编辑框不执行复制。
        if (!backend.IsEditorEmpty())
        {
            string backup = await backend.ReadEditorAsync().ConfigureAwait(false);
            string backupDir = ReportWriter.WriteReport(directory, code, backup, "before-input");
            string backupPath = Path.Combine(backupDir, "actual.txt");
            emitAction("backup", backupPath);
        }

        for (int attempt = 0; attempt <= options.Retries; attempt++)
        {
            backend.Check();
            double delay = Math.Min(options.Delay * Math.Pow(1.7, attempt), 1.0);
            emitAction("attempt", new Dictionary<string, object?>
            {
                ["attempt"] = attempt + 1,
                ["delay"] = delay
            });

            string actual;
            try
            {
                actual = await TypeOnceAsync(backend, code, options, delay, emitAction).ConfigureAwait(false);
            }
            catch (MismatchException error)
            {
                var extra = new Dictionary<string, object?>
                {
                    ["checked_prefix"] = error.Expected,
                    ["checked_line"] = error.Line,
                    ["checked_column"] = error.Column,
                    ["delay"] = delay,
                    ["attempt"] = attempt + 1
                };
                string reportPath = ReportWriter.WriteReport(directory, code, error.Actual, "mismatch", extra);
                emitAction("report", reportPath);

                if (attempt == options.Retries)
                {
                    throw;
                }

                emitAction("warning", $"{error.Message}；将降速重新输入。");
                continue;
            }

            var successExtra = new Dictionary<string, object?>
            {
                ["delay"] = delay,
                ["attempt"] = attempt + 1
            };
            string successReport = ReportWriter.WriteReport(directory, code, actual, "verified", successExtra);
            emitAction("report", successReport);

            return new TypingResult
            {
                Characters = code.Length,
                Lines = code.Split('\n').Length,
                ReportPath = successReport,
                Warning = warning
            };
        }

        throw new InvalidOperationException("未预期的执行终止。");
    }
}
