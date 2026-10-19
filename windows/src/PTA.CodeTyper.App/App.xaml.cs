// Copyright (c) 2026 PTA Code Typer contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;

namespace PTA.CodeTyper.App;

/// <summary>
/// 提供应用程序特定行为以补充默认 Application 类。
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>
    /// 初始化单例应用程序对象。
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 在应用程序正常启动时被调用。
    /// </summary>
    /// <param name="args">启动请求和过程的详细信息。</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    /// <summary>
    /// 获取当前活动的主窗口实例。
    /// </summary>
    public static MainWindow? CurrentWindow => (Current as App)?._window as MainWindow;
}
