// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using Xunit;
using PTA.CodeTyper.Core;

namespace PTA.CodeTyper.Core.Tests;

public class UrlValidationTests
{
    [Theory]
    [InlineData("https://pintia.cn/a", true)]
    [InlineData("https://www.pintia.cn/a", true)]
    [InlineData("http://pintia.cn/", true)]
    [InlineData("https://pintia.cn.evil.test", false)]
    [InlineData("https://evil.test/pintia.cn", false)]
    [InlineData("file:///pintia.cn", false)]
    [InlineData("https://pintia.cn@evil.test", false)]
    public void DomainValidation(string url, bool expected)
    {
        Assert.Equal(expected, TextNormalizer.IsValidPtaUrl(url));
    }
}
