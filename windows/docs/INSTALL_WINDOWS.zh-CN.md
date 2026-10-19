# PTA Code Typer Windows 安装说明

提供 Windows 11 x64 的 MSI 安装包与便携 ZIP。普通使用者无需编译；开发者可按 [本机构建指南](BUILD.zh-CN.md) 构建。应用已在维护者 Windows 电脑运行，最新 MSI 向导与快捷方式仍需首次安装时核对。

## 系统要求

- Windows 11 (21H2 或更新版本)
- x64 架构
- Microsoft Edge 或 Google Chrome

## 安装

1. 从 GitHub Releases 下载 `PTA-Code-Typer-<version>-windows-x64.msi`
2. 双击 MSI，按中文向导选择安装位置；默认按当前用户安装（通常不需要管理员权限），自选目录须有写入权限。
3. 安装位置：`%LocalAppData%\Programs\PTA Code Typer\`
4. 安装完成后，双击桌面 **PTA Code Typer** 快捷方式；开始菜单入口也保留。

## 首次运行

### Windows SmartScreen 提示

当前版本未使用付费代码签名。首次运行时，Windows SmartScreen 可能显示“已阻止不可识别的应用”：

1. 点击“更多信息”
2. 点击“仍要运行”

> **安全提示**：请仅从可信来源下载，并核对发布页提供的 SHA-256 校验和。

### 权限说明

- App 以普通用户权限运行，不需要管理员
- 不需要辅助功能权限（与 macOS 不同）
- 如果浏览器以管理员身份运行，App 将无法向其发送按键，请以普通用户身份重新打开浏览器

## 使用

1. 打开 App，点击“读取剪贴板”或“导入文件”载入代码
2. 明确选择 Microsoft Edge 或 Google Chrome（默认 Edge，已移除自动识别），再选择字符间隔、倒计时和校验方式
3. 切换英文输入法
4. 点击“开始输入”，App 暂时最小化
5. 在倒计时内点击 PTA 作答代码框
6. 输入结束后 App 自动恢复并显示结果

## 紧急停止

- 按 **F8**（或 Fn+F8）
- 将鼠标移到屏幕左上角
- Alt+Tab 切换到其他窗口
- 回到 App 点击“停止”

## 报告位置

输入记录保存在 `%LocalAppData%\PTA Code Typer\reports\`。
记录可能包含个人代码，请勿直接公开。

## 卸载

通过“设置 → 应用 → 已安装的应用”找到 PTA Code Typer 并卸载。
卸载会移除程序文件和快捷方式，保留用户报告。

## 故障排查

### App 无法启动

1. 确认系统为 Windows 11 x64
2. 检查是否被安全软件拦截
3. 尝试重新安装

### 输入无效

1. 确认已切换英文输入法
2. 确认浏览器以普通用户身份运行
3. 确认已点击 PTA 代码框正文区域
