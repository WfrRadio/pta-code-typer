# GitHub 双平台发布教程

当前仓库：https://github.com/WfrRadio/pta-code-typer 。已有仓库和 MIT 许可证，无需重新创建。

## 1. 准备

使用项目 `publish-ready/` 中的内容：

- `repository/`：已整理的双平台源码，不含运行记录、虚拟环境和构建缓存。
- `release-assets/`：macOS 应用 ZIP、Windows MSI 和便携 ZIP、双平台源码 ZIP、SHA256SUMS.txt。
- `RELEASE_NOTES.zh-CN.md`：可复制的中文发布说明。

源码放到仓库 Code，应用安装包放到 Releases。不要把整个开发目录直接上传，也不要把 ZIP 当作仓库唯一源码。Windows 的 wixpdb 为调试符号，本次无需上传。

## 2. 更新源码：分两次上传

GitHub 网页上传单文件上限 25 MiB，单次最多 100 个文件。本次双平台源码超过 100 个文件，需要分两次。

1. 在 Safari 打开仓库根目录，点击 **Add file → Upload files**。
2. Finder 打开 `publish-ready/repository/`，先将其中 **windows 之外的所有文件和目录**拖入上传区域。应看到 `readme.md`、`native/…`、`pta_typer/…` 等根目录路径，不能多套一层 repository。
3. Finder 按 Command+Shift+. 可以显示隐藏文件，将 `.gitignore` 一并上传。上传文件会覆盖仓库同路径旧文件。
4. Commit message 填 `Update macOS app and Chinese project documentation`。个人仓库可选择直接提交到 main；也可新建分支后合并。
5. 等第一次提交完成后，再从仓库根目录进入 **Add file → Upload files**，把整个 `windows` 文件夹拖进去，确保路径以 `windows/src/…` 等开头。
6. Commit message 填 `Add Windows WinUI 3 app and MSI installer`，提交。
7. 返回仓库根目录，核对 `native/`、`native_engine.py`、`windows/` 存在，README 显示双平台介绍，Windows 文档链接能打开。

请以拖动文件夹的方式保留目录层级。若 Safari 没有接受目录上传，可改用 GitHub Desktop：克隆当前仓库到一个新目录，将 repository 内的内容复制到克隆目录，检查 Changes 后 Commit to main，再 Push origin。不要初始化整个开发目录。

## 3. 修改 About 简介

仓库主页右侧 About 的齿轮中，将旧 macOS 简介改为：

> PTA（pintia.cn）逐字代码输入与回读校验工具，支持 macOS SwiftUI / Liquid Glass 与 Windows WinUI 3。

可添加 topics：`pta`、`pintia`、`macos`、`windows`、`swiftui`、`winui3`、`python`。

## 4. 创建双平台 Pre-release

1. 源码提交完成后，点击主页右侧 **Releases → Draft a new release / Create a new release**。
2. **Choose a tag** 输入 `cross-platform-preview-1`，选择创建新标签，Target 选择已更新后的 `main`。若该标签已经存在，改用未占用的新标签，不覆盖已有版本。
3. Release title 填 `双平台预发布 · macOS v0.2.0 / Windows v0.1.2`。
4. 把 `publish-ready/RELEASE_NOTES.zh-CN.md` 正文复制到说明框。
5. 将 `publish-ready/release-assets/` 内的 **5 个文件**拖到附件区：
   - macOS arm64 prerelease ZIP
   - Windows x64 MSI
   - Windows x64 portable ZIP
   - cross-platform-source ZIP
   - SHA256SUMS.txt
6. 等待所有附件上传完成，确认没有失败或仍在转圈的文件。
7. 勾选 **Set as a pre-release**。建议先 **Save draft**，检查附件名称、版本和说明后，再 **Publish release**。
8. 打开公开 Release 页面，确认 5 个附件都可见，README 的“下载安装包”能到达 Releases 列表。

两端应用内部版本不同，使用一个独立的双平台预发布标签即可，不必重新构建或强改版本。GitHub 自动生成的 Source code ZIP/tar.gz 是该标签下的仓库源码，不包含安装包；额外上传的双平台源码 ZIP 便于用户直接下载。

如果下载按钮指向 `/releases/latest`，可能略过预发布；本项目 README 使用 `/releases` 列表链接。

## 5. 发布后

分别下载检查安装包；Windows 首次安装重点核对安装位置和桌面快捷方式。不要为了上传重建 macOS App，以免临时签名改变后重新关联辅助功能权限。新版本使用新标签和新 Release，保留旧版本便于回退。

报告或 Issues 中避免附上个人账号、作业原文及完整运行目录。项目采用 MIT；保留 LICENSE 与第三方许可文件。

参考：[GitHub 上传文件](https://docs.github.com/en/repositories/working-with-files/managing-files/adding-a-file-to-a-repository) · [管理 Releases](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository)
