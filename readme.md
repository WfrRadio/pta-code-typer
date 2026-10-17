# PTA Code Typer（macOS）

一个带简单界面的 PTA 逐字输入工具。读取剪贴板或导入代码后，倒计时切到 PTA 作答代码框，自动输入并回读校验。

## 打开

普通使用者请在本仓库的 **Releases** 下载 `PTA-Code-Typer-v0.1.0-macOS-arm64-prerelease.zip`，解压后阅读包内“安装说明.md”，再打开 `PTA Code Typer.app`。

源码压缩包不包含可直接运行的 App。开发者自行构建后，可打开 `dist/PTA Code Typer.app`，或双击 `打开应用.command`。

打包版内置 Python 和 Tk，无需安装 Python、pip 或其他运行依赖。当前构建为 Apple Silicon（arm64）；Intel Mac 需在对应机器重新构建并测试。此本地构建使用临时签名，尚未做 Developer ID 签名与 Apple 公证，不是 App Store 安装包。

当前是 **v0.1.0 Pre-release**。App 需放在用户可写的固定文件夹中；不要单独移进系统 `/Applications`。首次下载可能被 macOS 拦截，确认来源后可按系统“隐私与安全性 → 仍要打开”的提示自行处理，然后另行开启辅助功能。详细步骤见 [安装说明](docs/INSTALL_MACOS.zh-CN.md)。

## 第一次使用

1. 在“环境与权限”页点击“打开辅助功能设置”，允许 **PTA Code Typer**。
2. 返回应用点“重新检测”。首次识别浏览器时，macOS 可能再询问自动化控制该浏览器的权限。
3. 打开浏览器中的 `pintia.cn` 作答页面，切换到英文输入法。
4. 在应用中“读取剪贴板”或“导入文件”（UTF-8），预览代码。
5. 选择浏览器、速度、倒计时和校验方式；默认自动识别、45 ms、7 秒、逐行校验。
6. 点击“开始输入”，应用会最小化；在倒计时内点击 PTA **可编辑的作答代码正文区域**。
7. 完成后应用返回并显示校验结果。只输入和校验，**不会点击提交或调用提交接口**。

辅助功能权限应给本应用；之前给 Codex 或终端的权限，不一定自动适用于独立应用。无需屏幕录制权限。

如果更新或重新构建后，系统开关已开启但应用仍显示未授权：先退出本应用，在辅助功能列表中移除旧的 PTA Code Typer，再用“＋”添加当前 `.app` 并开启，最后重新打开应用。本地临时签名在重新构建后可能变化，旧授权记录可能不再匹配。

## 停止与记录

- F8（部分 Mac 需 Fn+F8）、鼠标移到屏幕左上角，或切换到其他应用，都可停止输入。
- 也可回到应用点击“停止”；代码框中可能保留部分输入，请先检查。
- 替换前会备份非空编辑框；输入后保存原文、回读文本、SHA-256 和校验状态。
- 打包版记录位于 `.app` 同级的 `data/reports/`，源码运行时位于项目的 `data/reports/`。请放在有写权限的目录中。
- “运行记录”页可打开记录文件夹。所有代码仅本地处理；不要将 `data/` 上传到 GitHub。

## 支持范围

当前只做 macOS。已实现 Edge、Google Chrome、Brave、Chromium 的识别和原生输入适配；不能据此保证所有 Chromium 衍生浏览器都兼容。Firefox、Safari 暂不支持。

仅在当前标签页为 PTA 且焦点是可编辑多行代码框时开始；输入中焦点改变会停止。复制被禁用或剪贴板不稳定时会停止，不会将未验证的内容报为成功。输入法、网页更新或编辑器设置仍可能影响输入，最终以回读校验为准。

本程序规范化 CRLF、BOM、行首网页空格和缩进制表符，保留字符串内部内容。输入一致不代表答案正确或能在当前题型编译；函数题中包含 `main()` 时会给出提示。

已在本机 macOS arm64 + Edge 的 PTA 页面完成打包 App 实测：文件导入、剪贴板导入均通过，涵盖多种括号、引号、转义字符、宏换行、注释与中文；两组代码均全文回读一致。另有 13 项自动化测试通过。测试未提交答案。其他浏览器尚未实测。详细结果见 `tests/validation.json`。

## 源码与构建

源码启动需要 **Python 3.11+ 和可用的 tkinter/Tk**（Homebrew Python 不一定包含 tkinter）。也可双击 `源码启动.command`，它会检测环境并给出提示。

```sh
python3 -m venv .venv
.venv/bin/python -m pip install -r requirements-build.txt
.venv/bin/python main.py
.venv/bin/python -m unittest discover -s tests -v
.venv/bin/python scripts/build_macos.py
.venv/bin/python scripts/package_release.py
```

构建产物位于 `dist/`。PyInstaller 只用于构建，不是使用者需要安装的依赖。浏览器与系统接口均使用 macOS 原生框架，无需浏览器扩展或远程调试模式。

目录：`pta_typer/gui.py` 为界面，`core.py` 为输入与校验流程，`backends/macos.py` 为系统适配；`tests/` 为测试，`scripts/` 为构建工具。

本项目在独立目录创建，不依赖或修改上一级的 `pta.py`。源码采用 [MIT License](LICENSE)，第三方组件许可见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

源码与成品发布包由 `scripts/package_release.py` 按清单整理到 `release/`，不会把 `.venv`、运行记录或上一级文件混入源码包。发布前仍应核对清单、测试结果和第三方运行时版本。

第一次创建仓库和上传文件，请阅读 [GitHub 首次发布全流程](docs/GITHUB_FIRST_RELEASE.zh-CN.md)。Release 文案可直接复制 [v0.1.0 发布说明](docs/RELEASE_NOTES_v0.1.0.md)。
