# PTA Code Typer：Windows 开发交接与实施指南

交接对象：Antigravity。编写日期：2026-10-18。参考基线：本项目 macOS v0.2.0。

本文是 Windows 版本的实施要求和建议，不代表 Windows 版本已经开发或验证。现有 macOS 源码与实测记录已核对；Windows API、部署与设计资料见各节链接。开发时需再次核对所选工具的稳定版本与许可证。

## 1. 任务与首版决策

开发一个使用 **C# + WinUI 3 / Windows App SDK** 的 Windows 11 原生桌面应用。在用户已经打开的 **Microsoft Edge、Google Chrome** 中，向 PTA（pintia.cn）的作答代码框逐字输入代码。保留现有工具的全部基本功能，通过 GitHub Releases 提供安装包，不上架 Microsoft Store。

建议首版采用以下路线，先做输入验证，再做完整界面：

| 项目 | 首版选择 |
| --- | --- |
| 界面 | WinUI 3、XAML、Windows 11 Fluent Design |
| 输入引擎 | C# 实现；将现有 Python 引擎作为行为规范和回归参照 |
| 文本输入 | Win32 `SendInput` + `KEYEVENTF_UNICODE`，逐字符发送 |
| 浏览器读取与焦点识别 | Windows UI Automation（UIA），只读访问 |
| 安装 | unpackaged WinUI 3 应用，由按用户安装的 MSI 分发 |
| 运行依赖 | .NET 与 Windows App SDK 分别配置自包含，并核实其他原生依赖 |
| 权限 | 普通用户、`asInvoker`；不默认提权，不申请 UIAccess |
| 架构 | 先 Windows x64；ARM64 作为后续独立产物与实测目标 |
| 语言 | 简体中文优先，界面字符串集中管理 |

选择 C# 引擎是为了让 Windows 用户无需安装 Python，并减少跨进程打包、取消和剪贴板线程问题。不要只换界面却保留调用 macOS 框架的后端，也不要同时维护两套 Windows 输入引擎。若实验证明必须保留 Python helper，应先记录理由与部署代价，再调整方案。

**明确边界：**不自动提交答案、不自动点击运行或编译，不修改浏览器安全设置，不依赖浏览器扩展、远程调试端口或开发者工具。PTA 禁止粘贴，必须逐字输入；不可通过 DOM 赋值、UIA `SetValue`、JavaScript、Ctrl+V 或整段文本注入代替。读取 App 剪贴板和用 Ctrl+C 回读代码允许，但必须验证实际页面是否允许复制。

## 2. 给 Antigravity 的资料与阅读顺序

将**项目源码和本文一起**放到 Windows 工作目录，例如 `D:\Projects\pta-app`。交接机器上的本地路径不能写入 Windows 程序。若使用已有 v0.2.0 源码 ZIP，本文是之后新增的文件，须一并传递。

按以下顺序阅读，避免扫描历史二进制和用户代码：

| 文件 | 用途 |
| --- | --- |
| `readme.md` | 产品流程、现有功能、已知限制 |
| `pta_typer/core.py` | 规范化、逐字算法、备份、校验、重试和报告的权威实现 |
| `pta_typer/backends/macos.py` | 后端接口与保护逻辑的参考；系统调用不能直接移植 |
| `native_engine.py` | 状态事件、任务互斥、取消和结束清理的参考 |
| `native/PTACodeTyper.swift` | 三个页面、文件导入、剪贴板入口及交互参考 |
| `tests/test_core.py`、`tests/test_native_engine.py` | 可移植的行为约束 |
| `tests/test_macos_identity.py` | 浏览器身份与失败保护的思路；测试本身含 macOS 逻辑 |
| `tests/fixtures/basic.c`、`tests/fixtures/escapes.c` | 实际输入测试素材 |
| `tests/validation.json` | macOS 验证证据，不能当成 Windows 验证结果 |
| `LICENSE`、`THIRD_PARTY_NOTICES.md` | MIT 许可证及第三方许可基础 |

当前证据：macOS 有 29 项 App / 协议测试、11 项独立脚本回归测试；原生 App 已在 Safari 输入 171 个字符并全文一致，恢复了原有 778 个字符，未提交。Edge 有此前版本的实测记录；**现有 macOS Chrome 只有识别实现，没有现场通过记录。Windows Edge 与 Chrome 均需从头实测。**

不要公开或打包 `data/`、用户输入报告、原代码备份、剪贴板快照、`.venv/`、`build/`、`dist/`、历史 `release/`。不要修改或删除现有 macOS App、独立脚本、构建配置和发布包。Windows 实现放入新增的 `windows/`，共用素材优先只读引用；确需改共用文件时，说明原因并做原平台回归。

## 3. 所需 skill / 工具能力

这里的 skill 指执行任务所需的能力模块，**不是宣称 Antigravity 已安装这些同名插件**。先读取其环境里的可用技能目录和项目指令；有对应技能就使用，没有则依照下表与官方资料执行。不要寻找或依赖 Codex 专属绝对路径，也不要为了“装技能”运行来源不明的脚本。

| 能力模块 | 需要掌握的内容 | 应产出的证据 |
| --- | --- | --- |
| WinUI 3 原生开发 | C#、XAML、MVVM、DispatcherQueue、AppWindow、生命周期 | 能启动的原生窗口与三个页面 |
| Windows 11 设计 | Fluent、Mica、原生控件、键盘操作、Narrator、DPI | 明暗主题及缩放截图、无障碍检查 |
| Win32 输入与互操作 | SendInput、Unicode、P/Invoke 结构布局、按键释放、UIPI | Edge / Chrome 的逐字输入探针 |
| UI Automation | 焦点、文本模式、浏览器地址栏、COM 线程、元素失效 | 两浏览器的可读目标探针与拒绝测试 |
| 异步任务与取消 | CancellationToken、可取消等待、全局停止、资源释放 | 取消后无后续按键、无卡住修饰键 |
| 文本与剪贴板 | UTF-8、UTF-16、换行、STA、快照恢复与并发修改 | 原文一致性、失败关闭、剪贴板检查 |
| MSI 与部署 | 按用户安装、组件升级、自包含依赖、签名与卸载 | 干净 Windows 上可安装和升级的 MSI |
| 回归与 UI 实测 | 算法模拟编辑器、真实浏览器测试、打包后验收 | Windows 独立验证记录 |
| GitHub 发布 | Windows 构建、产物白名单、哈希、预发布文案 | 可复现源码、安装包、SHA256SUMS |

开发环境需要 Windows 11 的交互式桌面会话、Edge 和 Chrome、支持所选稳定 Windows App SDK 的 Visual Studio/Build Tools、对应 .NET SDK、Windows SDK、MSI 构建工具。以微软当前的 [WinUI 3 文档](https://learn.microsoft.com/en-us/windows/apps/winui) 与 [开发环境要求](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/system-requirements) 为准。锁定工具与 NuGet 版本，不把预览版 Gallery 示例的依赖自动带入正式产品。

macOS 上只能准备源文件与文档，不能据此宣称 WinUI、MSI 或浏览器输入通过。远程桌面断开、锁屏和无交互 CI 会影响输入；普通 Windows CI 可构建与跑逻辑测试，真实输入验收需交互会话。

## 4. 必须保持的基本功能

### 4.1 代码来源与预览

- 读取剪贴板文本、导入代码文件、手动编辑和核对预览；导入后保持内容，切换页面不丢失。
- UTF-8 / UTF-8 BOM 导入；解码失败明确提示，不悄悄用替换字符继续。其他编码可后续增加显式选择。
- 保持现有 **256 KiB UTF-8 字节**上限；文件和剪贴板入口都检查，不以 C# 字符串长度代替字节数。
- 显示字符数、行数和来源；配置“自动识别 / Edge / Chrome”、字符间隔、倒计时、校验间隔。
- 默认字符间隔 45 ms、倒计时 7 秒、逐行核对。对应范围：20～1000 ms、3～30 秒、每 1～20 行校验；最多 0～2 次重试，默认 1 次。
- 字符间隔不是总耗时；清理自动补全和回读也会耗时，不能把估算展示成承诺。

### 4.2 输入生命周期

读取 → 预览 → 开始 → 窗口暂时隐藏或最小化 → 倒计时内用户点击 PTA 作答代码正文 → 验证目标 → 备份原文 → 逐字输入 → 回读校验 → 结果与报告。

开始后禁止第二个任务和更改当前任务参数。使用不可变参数快照，输入源不随界面编辑变化。取消、错误和成功都要执行资源清理，恢复可用界面；返回 App 前确认所有按键已结束，避免 App 抢焦点打断最后一次校验。

状态至少区分 `Idle / Preparing / Countdown / ValidatingTarget / BackingUp / Typing / Verifying / Retrying / Completed / Cancelled / Failed`。保留原协议中的 `countdown / target / backup / attempt / progress / warning / report / done / error / cancelled / finished` 语义；全 C# 实现可以用类型化事件，无需照搬 JSON 管道。

### 4.3 停止与诊断

- 保留 F8、鼠标移到屏幕左上角、窗口“停止”、前台或焦点改变时停止。
- 使用可取消等待；停止后不能再发出下一字符或重试清空。组合键已按下时仍须释放本程序按下的键。
- 错误提示明确到“未开始 / 已部分输入 / 校验不一致”，避免只显示“失败”。
- 显示首个差异的行、列、预期/实际字符；全文一致只表示输入一致，不表示解答或编译正确。
- 函数题页面检测到完整 `main()` 时显示现有提醒；这是提示，不擅自删改源代码。

## 5. 核心算法：移植时最容易丢失的内容

### 5.1 文本规范化必须与现有实现一致

只去除开头 BOM，CRLF / CR 统一为 LF。每行**前导缩进**中的 NBSP、窄 NBSP 转为空格，Tab 按 4 列展开；不要替换字符串或注释内部的空白。拒绝非法控制字符与孤立代理项，不把字面的 `\n` 解释成换行。不擅自 trim 行尾空格、删除末尾换行、格式化代码或补括号。

C# 的 `string.Length` 计算 UTF-16 单元；与 Python 字符计数有差异。有效代理对必须允许，逐 Unicode 标量枚举并转成 UTF-16 输入；拒绝孤立代理项。差异行列和字符数需定义成一致规则，建议与原引擎的 Unicode 标量计数一致。报告哈希统一对规范化后的 UTF-8 字节计算 SHA-256。

### 5.2 清理自动补全的真实策略

现有算法并不是“打开括号后删除一个后括号”。PTA 可能补 `)`、`]`、`}`、引号、缩进甚至多行，因此要清除**光标后所有尚未输入的自动生成内容**。逐行流程如下：

1. 开始时先关闭补全弹窗，再选中作答编辑器全部内容并删除；此前备份必须已经成功落盘。
2. 每行先输入临时非空白字符 `x` 并清理尾部，避免直接输入 `}` 触发自动反缩进。
3. 逐字输入该行除前导空格外的内容；每个字符后等待、关闭补全弹窗、清除光标到文档末尾的自动生成内容。
4. 回到该逻辑行真正起点，删除临时 `x`，补上该行原有前导空格，再到逻辑行末尾。
5. 按设定间隔回读并比较“已完成源代码前缀”；最后必须比较完整文本。
6. 还有下一行时发送 Enter，清除自动缩进与自动生成的后续行，再继续。

这套算法依赖一个重要前提：编辑器中光标后的内容都是可丢弃的自动补全；用户旧代码已备份并清空。不要在未备份的原文上试清尾算法。

| 抽象操作 | Windows 候选实现，必须在 PTA 验证 |
| --- | --- |
| 全选、复制 | Ctrl+A、Ctrl+C，确认作用域是作答编辑器 |
| 删除、前删 | Backspace、Delete |
| 关闭补全 | Escape |
| 光标到文档末尾的选择 | Ctrl+Shift+End，然后 Delete |
| 清除新行已有缩进 | 清尾后选中当前行到真实起点，再 Delete |
| 行首、行尾 | Home / End 的逻辑行语义，必要时使用经过验证的组合操作 |

**不能机械替换 macOS 快捷键。**Home 可能在第一个非空白字符与列 0 之间切换，End 可能受视觉折行影响，Ctrl+Shift+End 可能触发平台快捷键。要用含缩进、空行、长行折行、自动生成多行括号的探针证明候选操作，再封装后端。禁止用全局按键映射试错用户未备份的代码。

### 5.3 回读与重试

优先探测 UIA `TextPattern` / `ValuePattern` 是否能**读取完整源文本**；只暴露隐藏 textarea、可视行或局部内容的结果不能当作全文。可采用现有 Ctrl+A / Ctrl+C 回读方式：临时标记剪贴板、等待新内容连续两次稳定、恢复光标和剪贴板。空代码框需要可靠识别；读不到不能直接当空白。

剪贴板必须考虑非文本格式和其他程序同时复制：能完整快照时恢复全部可用格式；无法安全快照时，优先 UIA 只读回读或在替换前明确停止。不能只保存字符串就声称保留了图片和文件。恢复前检查剪贴板序列号/所有权变化，避免覆盖用户新复制的数据；恢复失败应明确提示。所有回读路径都需清理临时标记。

两次回读仍不一致才判为 `Mismatch`；仅此类错误允许降速重输，间隔按原算法乘 1.7、上限 1 秒。每次重新清空前仍须通过目标保护。权限、焦点、剪贴板不可用、取消等错误直接停止，**不能通过重试继续向未知位置输入**。最终一次回读必须覆盖全文，即使配置每 20 行核对也不能省略。

## 6. Windows 输入、焦点与浏览器适配

### 6.1 SendInput 实现规范

普通字符使用 `KEYEVENTF_UNICODE`，`wVk=0`，`wScan` 放 UTF-16 单元；按下与抬起配对，有效代理对按序发送。Enter、Escape、导航键、Ctrl 组合键使用独立虚拟键操作；扩展键标志与 x64 结构对齐必须正确。不要用需要 Shift 状态的数字键序列生成括号。依据：[KEYBDINPUT](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-keybdinput)。

每次核对 `SendInput` 实际接受的事件数。开始前等待用户放开 Ctrl / Shift / Alt / Win；清理时只释放本程序确实按下的修饰键。API 返回成功仍不等于网页收到正确字符，以回读为准。Unicode 输入在中文输入法、输入法候选框和不同键盘布局下的行为是测试项，不能宣称完全不受输入法影响。

`SendInput` 只能向相同或更低完整性级别的进程注入，且失败返回不能准确说明是否被 UIPI 拦截。默认用普通用户运行 App 和浏览器；检测到浏览器提权时提示正常方式重开浏览器，不默认要求 App 管理员运行。依据：[SendInput 与 UIPI](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput)。

### 6.2 先实现目标探针，再输入

实现一个**只读诊断探针**，在 Edge 和 Chrome 分别证明以下信息可可靠取得：

1. `GetForegroundWindow`、目标顶层 HWND、PID、进程可执行文件身份；名称与路径用于识别，不能只匹配窗口标题或固定安装目录。
2. 当前活动标签页的完整 URL。优先通过目标窗口 UIA 地址栏只读获取，兼容中文/英文浏览器界面与不同安装通道；不能用网页标题推断域名。
3. 真正获得焦点的编辑器元素、所属文档、可编辑状态、完整读回能力。Chromium 的编辑器可能有隐藏输入区，不能照搬 macOS 的 `AXTextArea` 判断，也不能把 UIA `ValuePattern.IsReadOnly` 的单项结果当作完整证明。
4. 能区分作答框、题目输入示例、搜索框、地址栏、只读输出区域和其他标签页。

如果地址栏展示省略协议，必须定义并测试规范化规则；若无法得到可信 URL，应停止并记录 UIA 树与浏览器版本，修正定位方案。**不降级到“只要是 Edge 就能清空输入”。** Ctrl+L 会改变焦点，不应成为输入过程中反复检查 URL 的方案。

域名用 URI 解析后精确判断：`pintia.cn` 或其点分子域，scheme 为 http/https；拒绝 `pintia.cn.evil.test`、`pintia.cn@evil.test`、仅路径含 pintia.cn 的网页。一次任务锁定 HWND、PID、编辑器元素和文档标识。每次破坏性操作与按键前检查目标；以焦点/窗口事件配合限时 URL 与文档复核，检测同浏览器切标签、页面导航或元素失效。不能只检查 PID，因为同一浏览器内可以切到其他网站。

UIA 调用在独立 MTA 线程运行，不在 WinUI 界面线程同步遍历桌面；设置可控的调用超时与失效处理。UIA 或剪贴板调用阻塞时也不能导致停止按钮失效。必要时将可能挂起的探针隔离到可终止 helper，但应先验证确有需要。依据：[UI Automation 线程说明](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-threading)。

### 6.3 全局停止与会话边界

F8 可用 `RegisterHotKey` 加消息处理实现；若已被占用，明确显示并提供可验证的替代停止键，不能静默失效。停止监控独立于长 UIA 调用；检查鼠标紧急角落时使用主显示器物理坐标并考虑 DPI、多屏负坐标，不把整个负坐标显示器误判成停止区。

窗口切换、锁屏、会话断开、浏览器关闭、导航、用户改焦点均停止，不自动抢回焦点。输入时不弹抢焦点的进度窗口。用户主动点击 App 停止时可保留部分代码并展示备份入口；不要擅自恢复后覆盖用户刚做的新编辑。

## 7. Windows 11 原生界面指南

延续三个页面的工作流程，使用 Windows 原生视觉，不照搬 macOS Liquid Glass、红黄绿按钮或苹果图标。

- **代码输入：**NavigationView 侧栏；顶部 CommandBar 提供读取剪贴板、导入文件；主体等宽字体代码预览与设置；底部主按钮、状态和进度。
- **环境与检查：**显示浏览器检测、可用停止键、普通权限运行状态、版本和报告目录。Windows 通常不需要 macOS 式辅助功能授权，该页面不能虚构“开启辅助功能”开关。
- **运行记录：**结果、差异位置、打开报告与原文备份；提醒记录只在本地，不默认上传代码。
- 使用系统字体、主题资源、原生按钮/ComboBox/InfoBar/ProgressBar、标准标题栏和 Snap 行为；自定义标题栏保留拖动区与系统窗口控件。
- Mica 用于窗口基础层，编辑区保持高对比和稳定背景；透明效果关闭、节能与高对比模式时使用系统回退。依据：[Mica 官方指南](https://learn.microsoft.com/en-us/windows/apps/design/style/mica) 与 [NavigationView](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/navigationview)。
- 支持跟随系统明暗主题、100%/125%/150%/200% 缩放、较窄窗口、键盘 Tab 顺序和 Narrator 标签；结果不能仅靠颜色表达。
- App 内预览输入不要自动补全或改变缩进。首版可用支持多行的原生文本控件，关闭拼写/预测功能；暂不为语法高亮引入 WebView 编辑器。
- Ctrl+O 导入、读取剪贴板入口和主操作都有明确提示；不要让 App 内快捷键与运行中的全局停止冲突。

## 8. 安装、权限和签名：默认 MSI

### 8.1 为什么首选 MSI

WinUI 3 支持 unpackaged 部署，不要求 Microsoft Store。建议用自包含目录作为 MSI 载荷，安装在 `%LocalAppData%\Programs\PTA Code Typer\`，仅创建当前用户开始菜单快捷方式与卸载项。普通运行使用 `asInvoker`，无需 UIAccess、系统服务、驱动或辅助功能授权。企业策略仍可能限制安装，不能承诺所有电脑都无提示。[unpackaged 部署说明](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/unpackage-winui-app)

| 方案 | 用户体验与限制 | 本项目安排 |
| --- | --- | --- |
| 按用户 MSI | 正确配置时通常无需管理员；不必导入自签证书；仍可能有下载信誉或安全策略提示 | 首版必须交付 |
| 全机器 MSI | 写 Program Files / HKLM 通常涉及 UAC | 不作为默认路径 |
| 可信签名 MSIX | 可不上 Store，通过 App Installer 安装；需要可信签名和正确依赖 | 有签名条件后可选 |
| 自签 MSIX | 测试机须先信任证书，操作与企业策略限制较多 | 仅开发/测试，不作为最简公众安装 |

MSI 构建工具可选 WiX，先检查当前稳定版、开源维护费/使用条款和扩展许可是否符合本项目条件，再锁定版本；不能把商业扩展当作免费必需项。按用户安装的注册表、快捷方式、组件 key path 与升级必须统一作用域。参考：[Windows Installer 安装上下文](https://learn.microsoft.com/en-us/windows/win32/msi/single-package-authoring)、[WiX Package](https://docs.firegiant.com/wix/schema/wxs/package/)。

### 8.2 自包含与依赖验收

项目需分别考虑以下属性；这是配置方向，须以所选 SDK 的构建结果验证：

```xml
<WindowsPackageType>None</WindowsPackageType>
<SelfContained>true</SelfContained>
<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
<PublishSingleFile>false</PublishSingleFile>
```

RID 首版为 `win-x64`，固定一个仍受支持且与 WinUI 兼容的 .NET LTS 与 Windows App SDK 稳定组合，并写入 `global.json` / 包版本锁定。不要仅设置 `SelfContained` 就假定 Windows App Runtime 已包含。检查 Visual C++ Runtime 等原生依赖能否合法使用 app-local 分发；若必须安装机器级依赖，就不满足本方案“干净机器仅 MSI”的验收，应调整部署方案并明确说明，不能交一个缺依赖的 MSI。

保留多文件部署以便 MSI 维护，首版不追求单文件、不启用未经验证的 trimming / NativeAOT。在**没有 Visual Studio、Python、.NET SDK 和预装开发运行时**的标准用户 Windows 11 虚拟机上测试安装启动、离线启动和实际输入。

### 8.3 签名、更新和卸载

没有付费签名也可以提供传统 MSI 预发布，但不能保证 SmartScreen、Smart App Control、Defender 或组织策略允许；签名也不保证立即建立信誉。发布页如实说明签名状态与 SHA-256，不要求关闭系统保护。未来使用可信代码签名时，分别处理 EXE / MSI 签名与时间戳。

MSIX 公众分发通常要求设备信任其签名；自签包需要额外的信任步骤，不能宣称“双击即可，无需证书”。不自动安装根证书，不把开启开发者模式作为普通用户安装流程。参考：[MSIX 签名指南](https://learn.microsoft.com/en-us/windows/msix/package/sign-msix-package-guide)、[App Installer](https://learn.microsoft.com/en-us/windows/msix/app-installer/install-update-app-installer)。

MSI 固定 UpgradeCode，维护版本、ProductCode / PackageCode 和组件身份规则；验证升级只有一个安装项、一个当前快捷方式，保留设置和报告；失败应回滚。默认卸载移除程序和快捷方式，保留用户报告，并在说明中标明位置。不做静默联网自动更新，首版通过 GitHub 手动下载升级。

## 9. 建议目录与开发步骤

新增结构示例；不是声称这些文件现在已经存在：

```text
windows/
  PTA.CodeTyper.sln
  global.json
  Directory.Build.props
  src/
    PTA.CodeTyper.App/          # WinUI 3 界面、MVVM、应用生命周期
    PTA.CodeTyper.Core/         # 规范化、算法、状态、校验、报告
    PTA.CodeTyper.Windows/      # UIA、SendInput、剪贴板、停止监控
  tests/
    PTA.CodeTyper.Core.Tests/
    PTA.CodeTyper.Windows.Tests/
    fixtures/                  # 新增 Windows/编辑器专项素材
    validation.windows.json
  installer/                   # MSI 项目与固定升级身份
  scripts/                     # build/test/package/verify PowerShell
  docs/                        # 环境、安装、故障排查、发布说明
```

抽象接口建议：`IInputBackend`、`ITargetGuard`、`IClipboardService`、`IReportStore`；后端提供 `Text / SelectAll / Copy / Escape / Backspace / DeleteForward / Enter / LineStart / LineEnd / TrimTail / ClearAutoIndent / ReadEditor / CheckTarget`。系统行为放后端，核心算法可用模拟编辑器测试。所有持久文件写入 `%LocalAppData%\PTA Code Typer\`；安装目录不保存报告。

### 阶段 A：环境与可行性探针

1. 读取上述现有源码，记录行为基线与未测项；建立 Windows 新目录和版本锁定。
2. 最小 WinUI 3 程序在 Windows 11 启动；记录 OS、架构、SDK 与浏览器实际版本。
3. UIA 探针只读识别 Edge / Chrome 的 URL、作答焦点与文本；在本地测试页验证输入原语。
4. 在用户授权的 PTA 测试页备份原代码，输入短探针并回读，恢复原文；不提交。
5. 两浏览器均证明“可靠逐字输入、可信目标识别和全文回读”后再扩大实现。失败就先解决探针，不用漂亮界面掩盖后端未验证。

### 阶段 B：引擎与回归

1. C# 移植规范化、选项、报告、差异定位、取消和重试边界。
2. 实现模拟会自动补括号/引号/缩进的编辑器，验证临时 `x`、清尾与行首策略。
3. 实现 Windows UIA 与输入后端、剪贴板线程、目标守卫、全局停止。
4. 移植 Python 中有意义的行为测试；不把 macOS API 测试机械翻译成 Windows 假通过。

### 阶段 C：完整 WinUI 3 体验

连接三个页面、导入/剪贴板/预览、参数快照、倒计时与后台任务；用 DispatcherQueue 更新 UI。处理文件取消、超限、空剪贴板、任务中关闭窗口、重复开始、错误后重新运行。MTA UIA 与 STA 剪贴板任务要明确分工，不从工作线程直接调用 WinUI 控件。

### 阶段 D：打包后实测与交付

1. Release 构建、生成按用户 MSI，在干净虚拟机安装。
2. 从开始菜单打开**已安装 App**，点击开始，在 Edge 和 Chrome 逐项验收；不能只从 IDE 或测试后端运行。
3. 验证安装、升级、回滚、卸载、离线启动、标准用户权限和报告目录。
4. 生成源码包、MSI、校验和、第三方许可和预发布说明；保留真实未解决项。

## 10. 验收矩阵：以下不能省略

| 类别 | 必测场景 | 通过标准 |
| --- | --- | --- |
| 两浏览器 | Edge、Chrome 分别运行已安装 App 的 basic.c / escapes.c | 规范化后全文一致，原文恢复一致，无提交 |
| 括号/符号 | `() [] {} <>`、单/双引号、比较、逻辑、自增减、连续嵌套 | 不变数字，无多余后括号 |
| 自动补全 | 自动引号、缩进、多行大括号、注释星号、建议弹窗 | 无残留，缩进与原文一致 |
| C 源码 | `\0`、`\n`、`\t`、反斜杠、引号转义、宏续行、字符串括号 | 不解释转义、不改内容 |
| 文本边界 | BOM、CRLF/LF、前导 Tab/NBSP、中文、有效代理对、末尾空行 | 只做约定规范化，不截断 |
| 布局 | 长行视觉折行、横向滚动、编辑器字体缩放、页面缩放 | Home/End 与清尾仍正确 |
| 输入法 | 英文、中文输入法英/中模式、候选框、非美式键盘布局 | 支持则全文一致；不支持则开始前提示，不错报成功 |
| 错误目标 | 地址栏、搜索、题目示例、只读区、非 PTA、伪造域名 | 清空/输入前拒绝，原文不变 |
| 中途变化 | Alt+Tab、同浏览器换标签、导航、关闭窗口、锁屏 | 停止，无后续按键，无自动恢复覆盖 |
| 停止 | F8、App 停止、鼠标角落、热键占用 | 正确提示，按键释放，取消不重试 |
| 剪贴板 | 复制延迟/拒绝、非文本数据、其他程序复制、回读超时 | 不误判成功，不遗留标记、不盲目覆盖新剪贴板 |
| 重试 | 故意制造缺字/多字、焦点错误、取消、连续运行两次 | 只有文本差异降速重试，任务互斥 |
| 权限 | 普通用户、浏览器提权、组织限制 | 普通组合可用，其余给出准确可操作说明 |
| UI | 明暗、高对比、100～200% DPI、多屏、Narrator、键盘 | 可读可操作，焦点与停止正常 |
| 安装 | 无开发环境机器、离线、非管理员、升级/卸载 | 无隐含运行时依赖，无重复安装项，报告保留 |

用户之前曾报告“编译错误”，但粘贴的原代码与输入代码肉眼相同，因此验收必须保存机器可比较的全文与差异，而不是仅凭截图。代码题型或源程序本身错误不能归咎输入，也不能用“回读一致”宣称 PTA 编译通过。本任务不要求提交或编译验证。

运行时报告保存 `expected.txt / actual.txt / report.json`，包括状态、字符/行数、UTF-8 SHA-256、首差异、参数、尝试次数、浏览器版本和测试架构。完成替换前保存 `before-input` 原文。报告写入失败应在清空前停止，不能无备份继续。

Windows 验证汇总另存 `windows/tests/validation.windows.json`，记录每项 `passed / failed / not-tested`、安装包版本/哈希、测试来源（已安装 App 或源码）、OS/架构/浏览器版本、全文是否一致、恢复是否一致以及 `submitted: false`。公开汇总不含用户题目 URL、身份、代码与剪贴板；详细用户报告不随源码发布。

## 11. GitHub 构建与预发布交付

准备 Windows PowerShell 脚本，分别完成 restore/build/test/publish/MSI/package/verify；参数化配置、架构和输出目录，遇错返回非零。可用以下调用形态，但脚本与项目路径需由实现实际提供，不能将此示例当作已存在命令：

```powershell
# 在实现对应脚本后，从仓库根目录执行
./windows/scripts/build.ps1 -Configuration Release -Architecture x64
./windows/scripts/test.ps1
./windows/scripts/package.ps1 -Architecture x64
./windows/scripts/verify-package.ps1
```

如果仓库使用 GitHub Actions，新增 Windows 构建工作流；固定 SDK 和依赖版本，第三方 Action 固定到审核过的提交，默认最小权限。CI 不能登录 PTA、上传个人代码、输出签名密钥或声称完成交互式浏览器验证。发布权限仅给明确的发布步骤；签名凭据放 Secrets，不进源码 ZIP。

建议 Windows 首版用独立标签，例如 `windows-v0.1.0-alpha.1`，文件名明确 `windows-x64`，避免覆盖现有 macOS v0.2.0。MSI 数字版本由脚本从发布版本映射并校验，不把 SemVer 预发布字符串原样写入 MSI 数字字段。

最终交付至少包含：

- `PTA-Code-Typer-<version>-windows-x64.msi`：已验证安装包。
- `PTA-Code-Typer-<version>-source.zip`：白名单源码，含本文、构建说明和测试素材，不含个人数据或生成目录。
- `SHA256SUMS.txt`：与实际下载产物一致。
- Windows 安装/故障排查文档、版本工具清单、第三方许可、Windows 独立验证汇总。
- 发布说明：真实支持的 Windows 与浏览器版本、签名状态、安装步骤、已知问题、报告位置、无自动提交声明。

GitHub 网页流程：进入对应仓库 → Releases → Draft a new release → 选定目标提交并创建 Windows 标签 → 填写说明 → 上传 MSI、源码 ZIP 和 SHA256SUMS → 勾选 pre-release → 复核后发布。签名、证书、仓库创建/推送和公开发布依用户实际授权执行，不把文档交接当成已经授权发布。

安装后的最简用户流程应是：**下载 MSI → 双击按当前用户安装 → 开始菜单打开 → 导入或读取代码 → 点击开始 → 在倒计时内点击 PTA 作答正文 → 等待全文校验 → 用户自行决定提交。**

## 12. 可直接交给 Antigravity 的启动指令

> 请按照本文件开发 PTA Code Typer 的 Windows 11 版本。技术要求为 C# + WinUI 3，支持用户已打开的 Microsoft Edge 和 Google Chrome，在 PTA 作答代码框逐字输入，不允许用粘贴或 DOM 赋值替代。保留现有基本功能、自动补全清理、原文备份、全文回读、取消保护与报告。优先使用普通用户运行、自包含依赖、按用户安装的 MSI，不上架 Store。先读本文列出的现有代码，再在新增 windows/ 目录实现；不要破坏 macOS 版本。第一里程碑先证明两浏览器的 UIA URL/焦点/全文回读和短代码输入，再开发完整原生界面与安装包。所有 PTA 现场测试须有授权、先备份、测试后核对恢复，绝不提交。Windows 上实际验证已安装 App 后再声称兼容；遇到 SDK、运行时、UIA 或打包限制，记录证据、修正方案，不用模拟测试冒充真实通过。交付源码、MSI、校验和、安装指南、Windows 验证汇总和 GitHub 预发布说明。没有 Windows 交互环境时明确说明受阻部分，继续完成可验证的源代码工作，不声称已实测。
