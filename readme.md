# PTA Code Typer

**面向 PTA（pintia.cn）的本地逐字输入工具，支持 macOS 与 Windows。**

[下载安装包](https://github.com/WfrRadio/pta-code-typer/releases) · [反馈问题](https://github.com/WfrRadio/pta-code-typer/issues) · [MIT 许可证](LICENSE)

## 为什么做这个项目

在 PTA 作业平台中，教师可以限制代码粘贴。此时，在 IDE 中编写、调试和修改完成的代码无法直接粘贴到网页作答框，只能逐个字符重新输入，既耗时，也容易在括号、引号、缩进和转义字符上出错。

PTA Code Typer 从一个 Python 脚本发展而来，通过模拟键盘逐字输入，将本机代码录入 PTA 编辑器，并在输入结束后回读校验。它会处理编辑器自动补全后括号、自动缩进等常见干扰，让重复录入更方便。

macOS 版采用 **SwiftUI 与 Apple Liquid Glass**，通过内置 Python 组件完成输入；Windows 版采用 **C#、WinUI 3 与 Windows 11 原生界面**。两端独立实现系统适配，面向相同的录入流程。

这是独立开源项目，与 PTA 官方无隶属关系。请在课程和教师允许的使用范围内使用；工具只负责录入，不解题、不自动提交，也不能保证代码能够编译或答案正确。

## 平台与下载

| 平台 | 当前应用版本 | 系统要求 | 下载文件 |
| --- | --- | --- | --- |
| macOS | v0.2.0 预发布 | Apple Silicon，macOS 26+ | `PTA-Code-Typer-v0.2.0-macOS-arm64-prerelease.zip` |
| Windows | v0.1.2 预发布 | Windows 11，x64 | `PTA-Code-Typer-0.1.2-windows-x64.msi` |
| Windows 便携版 | v0.1.2 预发布 | Windows 11，x64 | `PTA-Code-Typer-0.1.2-windows-x64-portable.zip` |

两端均提供 Microsoft Edge、Google Chrome 适配；macOS 还提供 Safari 适配。浏览器和网页编辑器更新可能影响行为，遇到问题请反馈系统、浏览器及应用版本。macOS 的 Chrome 适配尚未完成现场实测；Edge、Safari 有既往实测记录。Windows 已由维护者在本机运行，最新 MSI 安装位置选择与快捷方式仍建议在首次安装时核对。

目前不提供 Intel Mac、Windows ARM64 或 Linux 成品。两端应用版本独立编号，不需要一致。

## 主要功能

- 从剪贴板读取代码，或导入本地代码文件并预览。
- 选择浏览器、输入间隔和倒计时，再切换到 PTA 代码框。
- 逐字输入，处理括号补全、缩进及引号等常见录入问题。
- 替换前备份原文，输入后回读校验并保存报告。
- 支持中止，完成后显示结果；不自动点击提交。

## 安装

### macOS

1. 下载并解压 macOS ZIP，将 `PTA Code Typer.app` 放到固定位置，例如“应用程序”。
2. 在“系统设置 → 隐私与安全性 → 辅助功能”中添加并启用该 App。
3. 首次访问浏览器时，按系统提示允许必要的自动化访问。

已内置 Python 输入组件，使用者无需安装 Python、pip 或浏览器扩展。当前没有付费 Developer ID 签名和 Apple 公证，首次下载可能被系统拦截。请核对来源与校验值，按照系统提供的提示处理；详见 [macOS 安装说明](docs/INSTALL_MACOS.zh-CN.md)。更新后若权限失效，可移除旧辅助功能条目，再添加实际运行的 App。

### Windows

推荐下载 **MSI**，按中文安装向导选择安装位置，安装后通过桌面或开始菜单快捷方式打开。默认安装到当前用户目录；自选目录须有写入权限。普通运行不需要管理员权限，也不需要 macOS 式的辅助功能授权。

便携版须先完整解压，再运行 `PTA Code Typer.exe`；不要单独移动 EXE，旁边的文件是运行依赖。Windows 版内置运行组件，使用者无需安装 Python 或 .NET SDK。浏览器和 App 均请以普通用户身份运行。

当前安装包未做商业代码签名，首次下载可能显示系统提示。详见 [Windows 安装说明](windows/docs/INSTALL_WINDOWS.zh-CN.md)。

## 使用步骤

1. 在 IDE 中准备好代码，打开 PTA 对应题目的作答页面。
2. 打开 App，读取剪贴板或导入文件，核对预览。
3. 选择目标浏览器；Windows 仅保留 **Microsoft Edge / Google Chrome**，默认 Edge。
4. 切换英文输入法，设置适当的字符间隔和倒计时。
5. 点击“开始输入”，倒计时内点击 PTA 可编辑代码框的正文区域；输入期间保持焦点。
6. 等待回读校验，检查代码与结果，再自行决定是否提交。

可按 **F8 / Fn+F8** 或返回 App 点击停止。切换其他应用、焦点异常等情况也可能停止输入。中止后网页可能保留部分代码，请检查。

输入一致只表示录入结果与准备的代码一致；编译错误、题型要求和算法正确性需要自行检查。若标点异常，先检查英文输入法并适当增加字符间隔。

## 本地数据

应用的代码输入流程在本地处理，无需项目账户或云端服务。报告和备份可能包含个人代码，请勿直接公开整个目录。

- macOS：`~/Library/Application Support/PTA Code Typer/reports/`
- Windows：`%LocalAppData%\PTA Code Typer\reports\`

## 开发与构建

### macOS

需要 Apple Command Line Tools、macOS 26+ SDK 与 Python 3.11+。

```sh
python3 -m venv .venv
.venv/bin/python -m pip install -r requirements-build.txt
.venv/bin/python -B -m unittest discover -s tests -v
.venv/bin/python scripts/build_macos.py
```

构建结果位于 `dist/`。[原生界面说明](docs/NATIVE_UI.zh-CN.md)。

### Windows

在 Windows 11 x64 上，从完整源码根目录双击 `windows/BUILD-WINDOWS.cmd`。脚本检查构建依赖，执行测试、发布和安装包生成；首次构建需联网获取官方依赖。普通使用者无需执行此步骤。

详见 [Windows 构建指南](windows/docs/BUILD.zh-CN.md) 和 [开发交接指南](docs/WINDOWS_DEVELOPMENT_HANDOFF.zh-CN.md)。

### 目录

| 路径 | 用途 |
| --- | --- |
| `native/` | macOS SwiftUI 界面 |
| `native_engine.py`、`pta_typer/` | macOS Python 输入与校验组件 |
| `standalone/` | macOS 独立脚本 |
| `windows/src/` | Windows WinUI 3 界面、核心与系统适配 |
| `windows/installer/`、`windows/scripts/` | Windows 安装包与构建工具 |
| `tests/`、`windows/tests/` | 测试与验证记录 |
| `docs/` | 中文安装、设计与发布文档 |

## 反馈与贡献

欢迎通过 Issues 描述复现步骤、期望行为和实际结果，附上系统、浏览器、应用版本及必要的脱敏日志。欢迎通过 Pull Request 提交改进。请勿上传个人作业、账号信息、整份运行报告或构建缓存。

## 开源许可

本项目采用 [MIT License](LICENSE)，允许在保留许可声明的前提下使用、修改和再分发。第三方组件保留各自许可证，详见 [第三方说明](THIRD_PARTY_NOTICES.md)。
