// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using System.Text.Json;
using Xunit;
using PTA.CodeTyper.Core;

namespace PTA.CodeTyper.Core.Tests;

public class ReportWriterTests
{
    [Fact]
    public void ReportSavedExactly()
    {
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var reportDir = ReportWriter.WriteReport(folder, "abc", "ab)", "mismatch");
            Assert.Equal("ab)", File.ReadAllText(Path.Combine(reportDir, "actual.txt")));
            var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(reportDir, "report.json")));
            Assert.Equal(3, json.RootElement.GetProperty("column").GetInt32());
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }
}
