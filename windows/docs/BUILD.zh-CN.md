# Windows 修正版：一键生成 EXE 与 MSI

当前版本为 0.1.2-preview.1。它不是已编译的安装包，完整编译必须在 Windows 11 x64 执行。

## 最简单的操作

1. 将新源码 ZIP **完整解压**到较短、非 OneDrive 同步的目录，例如 `C:\PTA`。不要从 ZIP 内直接双击，也不要继续使用旧的 0.1.0 源码包。
2. 进入解压后的 `windows` 文件夹，双击 `BUILD-WINDOWS.cmd`，无需以管理员运行。
3. 保持网络可用。脚本优先使用已安装的 .NET 8.0.4xx SDK；没有则把微软官方 x64 SDK 下载到项目 `.tools`。WiX 固定为 5.0.2，通过 NuGet 安装到项目 `.tools`，不写全局工具环境。
4. 依次执行测试、发布、完整依赖检查、递归文件收集与 MSI 构建。遇错立即停止并保留窗口和日志。

构建入口仅对当前 PowerShell 进程指定脚本执行策略，不修改系统或用户的持久策略；企业策略可能仍限制执行。不会上传源码、创建 GitHub 仓库或自动安装生成的 App。

## 成功后的文件

- `windows/publish/win-x64/PTA Code Typer.exe`：程序入口，必须连同同文件夹下所有 DLL、运行时和资源文件一起使用。**只复制一个 EXE 无法运行。**
- `windows/artifacts/PTA-Code-Typer-0.1.2-windows-x64-portable.zip`：完整可移动程序文件夹，解压后运行上述 EXE。
- `windows/artifacts/PTA-Code-Typer-0.1.2-windows-x64.msi`：按当前用户安装的安装包。
- `windows/artifacts/SHA256SUMS.txt`：两项分发文件的哈希。
- `windows/build-logs/build-时间.log`：全过程日志。

初版采用多文件自包含部署，便携 ZIP 就是可直接使用的 EXE 版本；不承诺无依赖的单文件 EXE。MSI 安装到当前用户的 LocalAppData/Programs，开始菜单有入口。浏览器与 App 均应普通权限运行，不需要 macOS 式辅助功能授权。

.NET SDK 与 NuGet 是构建依赖；用户安装生成的 App 时不应再安装 SDK。依赖检查和 MSI 生成不等于已完成干净机器启动、浏览器输入与卸载验收。

## 分步运行

在 `windows` 目录打开 64 位 PowerShell：

```powershell
.\scripts\test.ps1 -Configuration Release
.\scripts\publish.ps1 -Configuration Release -Architecture x64
.\scripts\package.ps1 -Architecture x64 -Version 0.1.2
```

分步模式需要系统已有 .NET 8 SDK；一键入口负责本地下载工具。当前只提供 x64，不再把未经配置/验证的 ARM64 显示为可选构建目标。

## 如果仍然报错

直接提供 `build-logs` 下最新日志中的**第一处错误及其上下文**，或完整文本日志。不要只复制最后的 Publish failed / XamlCompiler exit 1，因为它们可能是前面 C#、XAML 或资源错误的后续提示。

此前 `PreferredMinimumWidth/Height` 不存在、`MicaBackdrop` 引用和 `Windows.Graphics` 命名空间问题已经在此次整理的源码中消除。构建脚本全部保存为 ASCII + CRLF，避免 Windows PowerShell 5.1 按 ANSI 解读 UTF-8 中文时再次出现字符串终止符错误。

在某些机器缺少 Windows 资源构建任务时，仍可能需要对应的微软 Windows SDK/Visual Studio Build Tools 组件；以日志具体错误为准。不要通过修改文件扩展名、复制其他项目的 EXE、删除报错检查或全局关闭安全策略来制造成功结果。

## buildfix.2 修复的资源工具错误

若旧版出现 `MSB4062 / ExpandPriContent / Microsoft.Build.Packaging.Pri.Tasks.dll`，原因是只安装 .NET SDK 的机器没有 Visual Studio AppxPackage 任务。这是 buildfix.2 的历史方案：当时单独引用了 `Microsoft.Windows.SDK.BuildTools.MSIX` 1.2.20250107.1，随后发现其 targets 会混用 Windows App SDK 自带的旧 DLL。buildfix.4 已移除这项额外依赖，仅使用 Windows App SDK 内配套的 targets 与 DLL，同时保留 EnableMsixTooling=true。不要重新添加单独的 MSIX 工具包。**这只是构建工具，最终仍是 unpackaged EXE 与 MSI，不要求安装 MSIX、导入证书或上架 Store。**

已在同版本 .NET SDK 验证：实际 PRI 任务程序集可加载并执行空输入探针；MSBuild 求值中 `EnableCoreMrtTooling=false`、`WindowsPackageType=None`、`AppxPackage=false`。这不代表 Windows 完整 XAML 发布已成功，仍以 Windows 运行结果为准。

新版 `build-logs/session-时间/` 保存每次命令的 `stdout.log` 和 `stderr.log`；publish 另外有 `msbuild.log` 与 `binlog`。即使 PowerShell 主日志遗漏内容，也能取得原始编译错误。若再次失败，优先提供文件名含 `publish.stdout.log` 与 `publish.stderr.log` 的两个文本文件，不必重新安装整个 Visual Studio，也不要删除资源生成步骤。诊断日志保存在本地，不会自动上传。

资料：[微软资源构建工具包](https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools.MSIX/1.2.20250107.1)。

## buildfix.3

删除日志包装器重复追加的 `-v:normal`，避免 .NET CLI 报 `Option -v expects a single argument but 2 were provided`。已通过相同 PowerShell 包装器实际发布核心项目，验证参数和日志同时工作；此项不是 Windows 完整 WinUI 发布测试。

## buildfix.4：统一构建工具版本

本次 Windows 日志已出现 App DLL 输出，但在 WinAppSdkValidateConfiguration 加载时报错。原因已通过读取真实 DLL 类型表复现：外加 MSIX 工具的 44 项任务声明中有 21 项在旧 DLL 内不存在；改用 Windows App SDK 1.6.250205002 自带 targets 后，23 项任务声明全部能在配套 DLL 中找到。已移除独立 MSIX PackageReference，保留从同一 SDK 启用自带构建工具。真实项目还原/属性检查已通过，仍保持 WindowsPackageType=None、AppxPackage=false；完整 Windows 发布、启动与 MSI 安装仍待运行确认。

## v0.1.2：安装向导与浏览器选择

仅改 MSI 的安装体验与 App 浏览器选项：中文安装向导可选安装目录，安装后创建当前用户桌面快捷方式，保留开始菜单；浏览器只显示 Edge、Chrome，默认 Edge。逐字输入核心、Windows 后端及 SDK 构建配置与用户确认可运行的 FIX4 保持一致。

打包时自动从官方缓存还原与编译器完全同版本的 `WixToolset.UI.wixext/5.0.2`。使用项目本地 `.wix` 缓存，不安装全局扩展。用户默认安装目录通常无需提权；自选目录须有写入权限。向导使用项目 MIT 许可。

构建后优先分发 MSI，用户安装后从桌面启动，无需进入运行库文件夹找 EXE。本 Mac 无法完成新向导的 Windows 安装/升级实测，仍需在 Windows 安装生成的 0.1.2 MSI 验证。
