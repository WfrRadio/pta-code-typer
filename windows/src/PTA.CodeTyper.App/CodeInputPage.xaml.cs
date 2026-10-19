// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace PTA.CodeTyper.App;

/// <summary>
/// 代码输入、预览与启动页面。
/// </summary>
public sealed partial class CodeInputPage : Page
{
    public AppViewModel ViewModel { get; private set; } = null!;

    public CodeInputPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is AppViewModel vm)
        {
            ViewModel = vm;
            Bindings.Update();
        }
    }

    public bool InvertBool(bool value) => !value;
    public Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    public Visibility InvertVisibility(bool value) => value ? Visibility.Collapsed : Visibility.Visible;
    public string FormatStats(int chars, int lines) => $"{chars} 字符 · {lines} 行";

    public SolidColorBrush GetStatusColor(bool hasFailed, double progress)
    {
        if (hasFailed)
            return new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
        if (progress >= 1.0)
            return new SolidColorBrush(Microsoft.UI.Colors.ForestGreen);
        return new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
    }
}
