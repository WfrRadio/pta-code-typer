// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using Xunit;
using PTA.CodeTyper.Core;

namespace PTA.CodeTyper.Core.Tests;

public class OptionsTests
{
    [Theory]
    [InlineData(0.0, 7, 1, 1)]   // delay too low
    [InlineData(0.045, 0, 1, 1)] // countdown too low
    [InlineData(0.045, 7, 0, 1)] // check_every too low
    [InlineData(0.045, 7, 1, 5)] // retries too high
    public void RejectsInvalidValues(double delay, int countdown, int checkEvery, int retries)
    {
        var options = new Options { Delay = delay, Countdown = countdown, CheckEvery = checkEvery, Retries = retries };
        Assert.ThrowsAny<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void AcceptsDefaultValues()
    {
        new Options().Validate(); // should not throw
    }
}
