# Windows 代码审查与构建修复记录

日期：2026-10-18。仅修改 windows/，不触碰 macOS App 和现有 macOS 发布包，不上传仓库。

## 评价

已有三页 WinUI 界面、核心算法和后端分层，作为原型可以继续推进，但此前不能算已交付 Windows 版本。实际发布记录全部为 not-tested，安装脚本只是占位，且有输入保护漏洞。

## 已确认并修复

1. 原 package.ps1 没有调用 WiX，却打印完成；Product.wxs 的组件组没有文件，版本绑定还引用不存在的 MainExecutable。现在真实调用固定 WiX 5.0.2，递归打包全部发布依赖、稳定组件身份、HKCU key path、当前用户快捷方式与升级配置，只有实际 MSI 存在才成功。
2. 旧 ZIP 的 Mica 类型引用、过新版窗口 API 和相对 Windows.Graphics 命名空间造成用户所贴编译错误。修正版明确使用现有 WinUI 1.6 API，并以实际引用程序集检查 C# 类型；该检查不包含完整 XAML 编译。
3. PowerShell 中文编码导致第二次解析失败。七个构建脚本全部 ASCII + CRLF，微软 PowerShell 解析器检查无语法错误。
4. .NET global.json 不再漂移至任意大版本；核心库改为 net8.0，Windows 框架和自包含属性移到相应项目，原生结构单测通过 InternalsVisibleTo 获得访问权限。
5. 测试脚本原本要求未生成的 Debug 文件；现在构建并运行指定配置的测试项目。原重试单测没有实际执行/断言，已补成真实重试、备份先于清空、复制失败保护、取消不重试等测试。
6. 导航页设置 ViewModel 后更新 x:Bind；移除后台线程访问 WinUI HWND 的路径，F8 使用独立后台轮询。
7. 原代码复制标记未变化时误判为空代码框，会绕过旧代码备份。现在不把复制失败当作空白。
8. 原焦点校验缺失、异常或超时时放行，运行时也只检查 HWND/PID。现在初次校验失败直接停止，要求网页内编辑控件，并持续比较原编辑器运行时身份；移除从任意网页文本候选读取 URL 的降级路径。
9. 原 OpenClipboard(NULL) 后清空再写入没有正确剪贴板所有者。现在通过已依赖的 WPF/OLE 剪贴板写入并持久化，剪贴板忙时不假装读到空白。微软依据：https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-openclipboard 。

## 仍须 Windows 真实验收

- 严格 UIA 焦点规则需在实际 PTA 编辑器确认；识别不到时会拒绝开始，不能以忽略异常修复。
- 空编辑器、自动补全多行、Home/End 折行语义、中文输入法、焦点变化和锁屏需分别在 Edge/Chrome 验证。
- 当前回读保留的是纯文本剪贴板，不能声称保存图片、HTML 和文件等所有格式；测试前不要把剪贴板当作其他资料的唯一副本。完整多格式及外部剪贴板并发保护仍是后续改进。
- MSI 初次安装、干净机器离线启动、升级/卸载与系统运行时依赖需 Windows 验证。未购买可信签名，不能保证系统信誉/组织策略允许安装。
- Windows App SDK 1.6 是现有源码引用，修复版先控制变更，不把构建故障与大版本迁移混在一起；正式发布前需升级到受支持版本并重新验收。
- 未运行 Windows GUI/浏览器，不声称已产生可运行 EXE/MSI，也不把 Mac 上生成的 Windows DLL 当作 App 交付。

## 第二次 Windows 日志反馈与针对性修复

用户 Windows 日志证实：31 项核心测试、1 项 Windows 原生结构测试通过，发布阶段因 MSB4062 缺少 PRI 任务停止。旧的 PowerShell transcript 没有完整记录原始编译输出，补充文本才包含确切任务错误。

buildfix.2 启用 NuGet MSIX/PRI 构建工具及依赖，保持 WindowsPackageType=None 和禁止 Appx 打包。用真实项目验证已加载新任务程序集，并单独执行空输入的 WinAppSdkExpandPriContent 成功；没有宣称完成 Windows MakePri/XAML 编译。新增独立原生进程输出捕获，实测包含空格、引号、反斜杠路径的参数可正确传递，中文 stdout/stderr 均保存，子进程退出 7 会让构建失败；publish 自动生成 MSBuild 文本/二进制日志。

## 第四版工具版本统一

上一版独立 MSIX NuGet targets 与 Windows App SDK 自带 DLL 混用。本地检查复现用户日志中的 WinAppSdkValidateConfiguration 类型缺失，并发现这一组合共缺 21 项声明的任务。移除额外 MSIX 引用后，仅用 SDK 内配套工具，23 项任务类型全部匹配；项目还原与 PRI 预检查通过。此项仍不是完整 Windows 编译、启动或安装验收。
