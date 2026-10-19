# PTA Code Typer v0.1.2 — Safari 兼容预发布版

新增 macOS Safari 的 PTA 逐字输入支持，同时保留 Edge 及其他已适配浏览器的识别。

## 下载

- 普通用户：`PTA-Code-Typer-v0.1.2-macOS-arm64-prerelease.zip`
- 源码：`PTA-Code-Typer-v0.1.2-source.zip`
- 校验值：`SHA256SUMS.txt`

## 本次更新

- 浏览器选项新增 Safari，支持自动识别或手动选择。
- 按 Safari 的脚本字典读取当前标签页网址，继续限制为 pintia.cn 和可编辑作答代码框。
- 使用原生 Unicode 键盘事件逐字输入，并复制回读校验；无需开启“通过 Apple 事件运行 JavaScript”。
- 独立脚本支持 Edge / Safari 自动识别，也可用 `--browser safari` 指定浏览器。
- 保留 v0.1.1 对前台进程缓存和系统代码签名克隆路径的修复。
- 发布过程使用临时 App 副本，打包完成自动清理，项目内只保留 dist 中的运行版。

## 验证与限制

24 项 App 自动化测试、11 项独立脚本回归测试通过。Safari 的现场输入测试及打包版验证状态见 `tests/validation.json`。测试均不提交答案，完成后恢复原作答内容。

当前仅提供 Apple Silicon（arm64）版本。Edge 和 Safari 的实测范围见 README；Chrome、Brave、Chromium 尚未实测。没有自动更新。

本包采用临时签名，没有 Developer ID 签名和 Apple 公证。更新构建后可能需要移除旧的辅助功能授权，再添加新的 App；首次控制 Safari 时也可能需要允许自动化访问，用于读取标签页网址。

保持整个 App 所在目录可写，记录仍保存在旁边的 `data/reports/`。安装步骤见包内“安装说明.md”。
