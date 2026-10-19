// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace PTA.CodeTyper.App;

/// <summary>
/// 显示环境检测与运行状态的页面。
/// </summary>
public sealed partial class EnvironmentPage : Page
{
    public AppViewModel ViewModel { get; private set; } = null!;

    public EnvironmentPage()
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
}
