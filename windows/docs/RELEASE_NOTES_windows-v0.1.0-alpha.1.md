# PTA Code Typer · Windows v0.1.0-alpha.1 发布说明

Windows 11 原生版本首个预发布。使用 C# + WinUI 3 开发，支持在用户已打开的 Microsoft Edge 和 Google Chrome 中向 PTA (pintia.cn) 作答代码框逐字输入代码。

## 下载

| 文件 | 说明 |
| --- | --- |
| `PTA-Code-Typer-0.1.0-windows-x64.msi` | Windows x64 安装包 |
| `PTA-Code-Typer-0.1.0-source.zip` | 源码包 |
| `SHA256SUMS.txt` | 文件校验和 |

## 系统要求

- Windows 11 (21H2+), x64
- Microsoft Edge 或 Google Chrome

## 安装

双击 MSI 文件，按当前用户安装。无需管理员权限、无需安装 .NET 或其他运行时。

## 功能

- 读取剪贴板 / 导入 UTF-8 代码文件
- 预览和编辑代码
- 自动识别 Edge / Chrome 浏览器
- 倒计时后逐字输入，自动清理编辑器自动补全
- 逐行回读校验，不一致时降速重试
- 原内容先备份再替换
- 多种紧急停止方式（F8、鼠标角落、窗口切换）
- Mica 材质 + Fluent Design，跟随系统明暗主题

## 签名状态

当前版本未使用付费代码签名。首次运行可能触发 Windows SmartScreen 提示。
请从可信源下载并核对 SHA-256 校验和。

## 已知问题

- 这是首个预发布版本，尚未完成所有浏览器和场景的实测验证
- ARM64 尚未支持
- 中文输入法环境下的行为需要进一步测试

## 重要声明

- 本 App **不会自动提交答案**
- 所有代码仅在本机处理
- 输入一致不代表编译正确或解题正确
- 报告位置：`%LocalAppData%\PTA Code Typer\reports\`
