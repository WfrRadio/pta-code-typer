# Windows 分发的第三方组件

PTA Code Typer 项目代码采用仓库根目录 MIT License，构建时随程序复制为 LICENSE.txt。

Windows 程序依赖并可能自包含分发以下运行组件，须保留各组件随包提供的版权、许可及第三方声明：

| 组件 | 来源与许可资料 |
| --- | --- |
| .NET 8 / Windows Desktop Runtime、WPF 与 UI Automation 引用 | https://github.com/dotnet/runtime/blob/main/LICENSE.TXT ； https://github.com/dotnet/wpf/blob/main/LICENSE.TXT ； https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT |
| Windows App SDK / WinUI 3（当前引用 1.6.250205002） | https://github.com/microsoft/WindowsAppSDK/blob/main/LICENSE ； https://github.com/microsoft/microsoft-ui-xaml/blob/main/LICENSE |

系统 user32/kernel32 等 Windows 组件不从开发机复制到安装包。Microsoft Windows SDK / BuildTools 的许可由微软提供；作为构建工具与可再分发组件的条件应分别核对。

WiX 5.0.2 只在构建机生成 MSI，不包含在 App 运行文件中。其源码与许可：https://github.com/wixtoolset/wix/tree/v5.0.2 。xUnit、Moq、Microsoft.NET.Test.Sdk 只用于测试，不随程序打包。

本 Windows 版本没有捆绑 Python 或 Tcl/Tk，macOS 版的 Python/Tk 许可清单不能直接当作 Windows 运行组件清单。正式分发前仍应核对最终发布目录内真实 DLL 与各供应商许可证，特别是后续增加的 VC++ Runtime 等组件。
