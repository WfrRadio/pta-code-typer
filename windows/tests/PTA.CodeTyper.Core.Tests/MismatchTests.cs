// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using Xunit;
using PTA.CodeTyper.Core;

namespace PTA.CodeTyper.Core.Tests;

public class MismatchTests
{
    [Fact]
    public void DiagnosticLocation()
    {
        var ex = new MismatchException("a\n(1);", "a\n91);");
        Assert.Equal(2, ex.Line);
        Assert.Equal(1, ex.Column);
    }

    [Fact]
    public void FunctionProblemWarning()
    {
        Assert.NotEmpty(TextNormalizer.GetTaskWarning(
            "https://pintia.cn/problem-sets/1/exam/problems/type/6", "int main() {}"));
        Assert.Empty(TextNormalizer.GetTaskWarning(
            "https://pintia.cn/problem-sets/1/exam/problems/type/7", "int main() {}"));
    }
}
