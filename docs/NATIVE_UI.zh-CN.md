# 原生 SwiftUI 与 Liquid Glass

v0.2.0 将正式 App 界面改为 SwiftUI，使用系统 NavigationSplitView、侧栏、工具栏、原生文件选择器、SF Symbols 和玻璃按钮。底部操作区使用 `glassEffect`，代码内容区保留清晰的不透明文本背景。

等宽编辑器基于 NSTextView，关闭智能引号、智能破折号和自动文本替换。布局跟随窗口大小调整，并跟随系统深浅色。导航、导入、编辑和开始按钮提供原生可访问名称与键盘入口。

代码输入继续使用经过现场验证的 Python 引擎，通过私有子进程的 stdin/stdout JSON 行协议通信。取消通过事件传到输入线程；界面不会在输入线程结束前抢回前台。

构建使用 Swift 6.3.3 / macOS SDK 26.5，最低部署 macOS 26.0，已在 macOS 26.6.2 查看窗口与文件导入。没有 macOS 27 SDK 或真机验证，因此不能声称完成 macOS 27 专属适配。系统标准组件会跟随运行系统的外观。

设计与 API 依据：

- https://developer.apple.com/design/human-interface-guidelines/materials
- https://developer.apple.com/documentation/technologyoverviews/adopting-liquid-glass
- https://developer.apple.com/documentation/swiftui/applying-liquid-glass-to-custom-views

构建：`python scripts/build_macos.py`。源代码、构建环境和运行环境的具体要求见 README。
