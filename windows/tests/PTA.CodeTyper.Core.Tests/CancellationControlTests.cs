// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using Xunit;
using PTA.CodeTyper.Core;

namespace PTA.CodeTyper.Core.Tests;

public class CancellationControlTests
{
    [Fact]
    public async Task CancelledWaitThrows()
    {
        var control = new CancellationControl();
        control.Cancel();
        await Assert.ThrowsAsync<CancelledException>(() => control.WaitAsync(10));
    }

    [Fact]
    public void CancelledCheckThrows()
    {
        var control = new CancellationControl();
        control.Cancel();
        Assert.Throws<CancelledException>(() => control.Check());
    }
}
