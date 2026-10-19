// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using Moq;
using Xunit;
using PTA.CodeTyper.Core;

namespace PTA.CodeTyper.Core.Tests;

public class TypingEngineTests
{
    [Fact]
    public async Task FocusErrorBeforeAnyTyping()
    {
        var backend = new Mock<IInputBackend>();
        backend.Setup(b => b.LockTargetAsync(It.IsAny<string>())).ThrowsAsync(new StoppedException("wrong focus"));
        await Assert.ThrowsAsync<StoppedException>(
            () => TypingEngine.RunTypingAsync(backend.Object, "a", new Options(), "/not-used", countdown: false));
        backend.Verify(b => b.SelectAllAsync(), Times.Never());
        backend.Verify(b => b.BackspaceAsync(), Times.Never());
    }

    [Fact]
    public async Task MismatchRetriesThenVerifies()
    {
        using var control = new CancellationControl();
        var backend = new Mock<IInputBackend>();
        backend.SetupGet(b => b.TargetUrl).Returns("https://pintia.cn");
        backend.SetupGet(b => b.BrowserName).Returns("Edge");
        backend.Setup(b => b.IsEditorEmpty()).Returns(true);
        backend.Setup(b => b.Control).Returns(control);
        backend.SetupSequence(b => b.ReadEditorAsync()).ReturnsAsync(")").ReturnsAsync(")").ReturnsAsync("a");
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            var result = await TypingEngine.RunTypingAsync(backend.Object, "a", new Options { Delay = .02, Retries = 1 }, folder, countdown: false);
            Assert.Equal(1, result.Characters);
            backend.Verify(b => b.BackspaceAsync(), Times.Exactly(2));
            Assert.Equal(2, Directory.GetFiles(folder, "report.json", SearchOption.AllDirectories).Length);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task BackupIsOnDiskBeforeClearing()
    {
        using var control = new CancellationControl();
        var backend = new Mock<IInputBackend>();
        backend.SetupGet(b => b.Control).Returns(control);
        backend.SetupGet(b => b.TargetUrl).Returns("https://pintia.cn");
        backend.Setup(b => b.IsEditorEmpty()).Returns(false);
        backend.SetupSequence(b => b.ReadEditorAsync()).ReturnsAsync("old code").ReturnsAsync("a");
        var folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        backend.Setup(b => b.BackspaceAsync()).Callback(() =>
            Assert.Contains(Directory.GetFiles(folder, "actual.txt", SearchOption.AllDirectories), p => File.ReadAllText(p) == "old code"))
            .Returns(Task.CompletedTask);
        try { await TypingEngine.RunTypingAsync(backend.Object, "a", new Options { Delay = .02 }, folder, countdown: false); }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task UnreadableBackupNeverClearsOrRetries()
    {
        var backend = new Mock<IInputBackend>();
        backend.SetupGet(b => b.TargetUrl).Returns("https://pintia.cn");
        backend.Setup(b => b.IsEditorEmpty()).Returns(false);
        backend.Setup(b => b.ReadEditorAsync()).ThrowsAsync(new StoppedException("copy unavailable"));
        await Assert.ThrowsAsync<StoppedException>(() => TypingEngine.RunTypingAsync(backend.Object, "a", new Options { Retries = 2 }, "/not-used", countdown: false));
        backend.Verify(b => b.BackspaceAsync(), Times.Never());
    }

    [Fact]
    public async Task CancelDuringInputNeverRetries()
    {
        using var control = new CancellationControl();
        var backend = new Mock<IInputBackend>();
        backend.SetupGet(b => b.Control).Returns(control);
        backend.SetupGet(b => b.TargetUrl).Returns("https://pintia.cn");
        backend.Setup(b => b.IsEditorEmpty()).Returns(true);
        backend.Setup(b => b.TextAsync("x")).Callback(control.Cancel).Returns(Task.CompletedTask);
        await Assert.ThrowsAsync<CancelledException>(() => TypingEngine.RunTypingAsync(backend.Object, "a", new Options { Retries = 2 }, "/not-used", countdown: false));
        backend.Verify(b => b.BackspaceAsync(), Times.Once());
        backend.Verify(b => b.TextAsync("a"), Times.Never());
    }
}
