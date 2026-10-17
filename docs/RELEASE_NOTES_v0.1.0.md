# PTA Code Typer v0.1.0 — macOS 公开测试版

读取剪贴板或导入 UTF-8 代码文件，在 PTA 作答代码框中逐字输入并回读校验。

## 下载

- 普通使用者：`PTA-Code-Typer-v0.1.0-macOS-arm64-prerelease.zip`
- 开发者：`PTA-Code-Typer-v0.1.0-source.zip`，或查看本仓库源码。
- 校验文件：`SHA256SUMS.txt`

App 内置 Python 与界面依赖。源代码采用 MIT 许可，第三方许可随包提供。

## 功能

- 剪贴板读取、文件导入、预览与编辑。
- 速度和倒计时选项、权限引导、运行进度。
- Unicode 逐字输入、补全括号与缩进处理、逐行/分段回读核对。
- 覆盖前备份，焦点变化停止，F8 与鼠标左上角紧急停止。
- 不自动提交答案。

## 已验证与限制

- 已在 macOS 26.6.2 arm64 + Microsoft Edge 的 PTA 页面实测，文件和剪贴板两条入口的代码均全文一致。
- 13 项核心自动化测试通过；未提交测试答案。
- Chrome、Brave、Chromium 已加入识别，尚未实测；其他系统版本也未验证。
- 不含 Intel Mac、Windows 版本，不含自动更新。
- 使用临时签名，没有 Developer ID 签名或 Apple 公证。首次打开可能需在系统“隐私与安全性”中选择“仍要打开”，然后另行授权辅助功能。
- 当前为便携式目录版：放在用户可写的固定文件夹，记录保存在 App 旁的 `data/reports/`；不要单独拖入系统 `/Applications`。

下载后请先阅读包内 `安装说明.md`。输入校验通过不代表代码能在当前题目编译或通过评测。
这是 Pre-release，欢迎在 Issues 中报告问题；请勿公开包含个人代码的完整运行记录。
