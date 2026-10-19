// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace PTA.CodeTyper.App;

/// <summary>
/// 显示运行记录与校验日志的页面。
/// </summary>
public sealed partial class ActivityPage : Page
{
    public AppViewModel ViewModel { get; private set; } = null!;

    public ActivityPage()
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

    public Visibility IntToEmptyVisibility(int count) => count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IntToListVisibility(int count) => count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public static SolidColorBrush GetLogColor(bool isError)
    {
        return isError
            ? new SolidColorBrush(Microsoft.UI.Colors.OrangeRed)
            : new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
    }
}
