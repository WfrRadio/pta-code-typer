// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace PTA.CodeTyper.Core;

/// <summary>
/// 本地运行记录与差异报告写入器。
/// </summary>
public static class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// 在指定目录下创建时间戳子目录，写入 expected.txt、actual.txt 和 report.json。
    /// </summary>
    /// <param name="directory">存放报告的主目录。</param>
    /// <param name="code">预期的代码文本。</param>
    /// <param name="actual">实际读取到的代码文本（可为 null）。</param>
    /// <param name="status">执行状态标识，如 "verified"、"mismatch"、"before-input" 等。</param>
    /// <param name="extra">附加到 report.json 的元数据键值对。</param>
    /// <returns>创建的报告子文件夹路径。</returns>
    /// <exception cref="ArgumentNullException">当必要参数为 null 时抛出。</exception>
    public static string WriteReport(
        string directory,
        string code,
        string? actual,
        string status,
        IReadOnlyDictionary<string, object?>? extra = null)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(status);

        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-ffffff");
        string folder = Path.Combine(directory, timestamp);
        Directory.CreateDirectory(folder);

        string expectedPath = Path.Combine(folder, "expected.txt");
        File.WriteAllText(expectedPath, code, Encoding.UTF8);

        if (actual != null)
        {
            string actualPath = Path.Combine(folder, "actual.txt");
            File.WriteAllText(actualPath, actual, Encoding.UTF8);
        }

        var summary = new Dictionary<string, object?>
        {
            ["status"] = status,
            ["expected_chars"] = code.Length,
            ["actual_chars"] = actual?.Length,
            ["expected_sha256"] = ComputeSha256Hex(code),
            ["actual_sha256"] = actual != null ? ComputeSha256Hex(actual) : null
        };

        if (extra != null)
        {
            foreach (var kvp in extra)
            {
                summary[kvp.Key] = kvp.Value;
            }
        }

        if (actual != null && actual != code)
        {
            var error = new MismatchException(code, actual);
            summary["line"] = error.Line;
            summary["column"] = error.Column;
            summary["difference"] = error.Message;
        }

        string reportJsonPath = Path.Combine(folder, "report.json");
        string json = JsonSerializer.Serialize(summary, JsonOptions);
        File.WriteAllText(reportJsonPath, json, Encoding.UTF8);

        return folder;
    }

    private static string ComputeSha256Hex(string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        byte[] hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
