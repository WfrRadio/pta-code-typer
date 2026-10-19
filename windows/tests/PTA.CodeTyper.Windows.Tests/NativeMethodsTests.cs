// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using Xunit;
using PTA.CodeTyper.Windows;
using System.Runtime.InteropServices;

namespace PTA.CodeTyper.Windows.Tests;

public class NativeMethodsTests
{
    [Fact]
    public void InputStructSizeIsCorrect()
    {
        // INPUT struct must be correctly sized for SendInput
        var size = Marshal.SizeOf<NativeMethods.INPUT>();
        // On x64, INPUT is 40 bytes (4 type + 4 padding + 32 union)
        Assert.Equal(40, size);
        Assert.Equal(8, Marshal.OffsetOf<NativeMethods.INPUT>("ki").ToInt32());
    }
}
