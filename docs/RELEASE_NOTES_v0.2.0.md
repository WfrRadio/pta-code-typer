# PTA Code Typer v0.2.0 — 原生 SwiftUI / Liquid Glass 预发布版

## 下载

- App：`PTA-Code-Typer-v0.2.0-macOS-arm64-prerelease.zip`
- 源码：`PTA-Code-Typer-v0.2.0-source.zip`
- 校验文件：`SHA256SUMS.txt`

## 更新

- 正式 App 改用原生 SwiftUI：系统侧栏、工具栏、文件导入、等宽编辑器和 Liquid Glass 操作区。
- 保留 Safari / Edge 输入兼容、Unicode 逐字输入、覆盖前备份、回读校验与紧急停止。
- 记录改存用户 Application Support，可将 App 安装到“应用程序”。
- Python 输入组件已内置；使用者无需 Python 或 Tk。
- 发布只保留一份运行版 App，暂存副本在打包后清理。

## 要求与验证

要求 Apple Silicon、macOS 26 或更新版本。使用 SDK 26.5 构建，在 macOS 26.6.2 查看并测试；尚未在 macOS 27 真机验证。

29 项 App / 协议测试和 11 项独立脚本测试通过。Safari 的源代码输入、独立脚本和实际打包输入组件均已现场校验；完整原生窗口启动输入的状态以 `tests/validation.json` 为准。所有现场测试不提交答案，并恢复原内容。

Chrome、Brave、Chromium 已加入识别但尚未实测；没有自动更新。本版为 Pre-release。

仍为临时签名，没有 Developer ID 签名或 Apple 公证。首次下载可能需要“隐私与安全性 → 仍要打开”。原生主程序与旧版不同，更新后可能需要移除旧辅助功能条目，重新添加当前 App。首次控制 Safari 可能需要允许读取标签页的自动化访问。

详见包内“安装说明.md”。
