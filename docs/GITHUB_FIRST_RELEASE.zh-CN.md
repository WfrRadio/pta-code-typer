# 第一次在 GitHub 开源与发布：全流程

你只需要浏览器、GitHub 账户和本次整理好的两个压缩包，不需要先安装 Git。
本文不替你登录、创建仓库或公开上传；最后发布由你操作。

## 1. 注册或登录

访问 https://github.com/ 。没有账户时自行注册并完成邮箱验证，再登录。
GitHub 账户与 Apple Developer 账户无关。

## 2. 创建公开仓库

访问 https://github.com/new ，填写：

- Owner：自己的 GitHub 账户。
- Repository name：建议 `pta-code-typer`。
- Description：`macOS PTA code typer with character-by-character input and verification.`
- Visibility：选择 **Public**。
- 不额外添加 README、.gitignore 或许可证：源码包已经带齐这些文件。
- 点击 **Create repository**。

仓库是项目的源码主页；Release 是这个项目某个版本的成品下载页。
公开源码并附上 `LICENSE` 即可按该许可共享，不存在另一个“开启开源”的开关。
本项目采用 MIT，别人可以在保留声明等许可条件下使用、修改和分发。

官方说明：https://docs.github.com/en/repositories/creating-and-managing-repositories/creating-a-new-repository

## 3. 上传源码文件

1. 在 Mac 上解压 `PTA-Code-Typer-v0.2.0-source.zip`。
2. 打开解压后的 `PTA-Code-Typer-v0.2.0` 文件夹。
3. 在 Finder 按 **Command + Shift + .** 显示隐藏文件，确保能看到 `.gitignore`。
4. 回到新建的 GitHub 仓库，点击 **uploading an existing file**；已有内容的仓库则点 **Add file → Upload files**。
5. 拖入该文件夹**内部的文件和子文件夹**，保留子文件夹结构；不要把外层文件夹再套一层，也不要只上传 ZIP。
6. 上传列表里应能看到根目录的 `main.py`、`readme.md`、`LICENSE`、`.gitignore`，以及 `pta_typer/`、`scripts/`、`tests/`、`assets/`、`docs/`、`licenses/` 等。
7. 不应出现 `.venv/`、`data/`、`build/`、`dist/`、`release/` 或个人作答备份。本次源码包已按清单排除这些内容；网页上传不会替你按 `.gitignore` 过滤。
8. Commit message 填 `Initial open-source release`。首次上传可直接提交到默认分支（通常是 `main`）。
9. 按界面完成 **Commit changes**。若界面创建了分支并显示 **Propose changes**，再建立 Pull request，并在仓库中合并它，让文件进入默认分支。
10. 返回仓库首页，确认 README 正常显示、能进入 `pta_typer` 目录、能打开 `LICENSE`。

GitHub 不会把你上传到源码列表里的 ZIP 自动展开成代码。因此，源码页上传解压后的文件；Release 附件才上传 ZIP。
网页每批最多 100 个文件，单文件上限 25 MiB。本次源码包小于这些限制；后续规模增加可改用 GitHub Desktop 或 Git。

官方说明：https://docs.github.com/en/repositories/working-with-files/managing-files/adding-a-file-to-a-repository

## 4. 创建 Pre-release 草稿

1. 在仓库首页右侧找到 **Releases**，点击 **Create a new release** 或 **Draft a new release**。
2. 在 **Choose a tag** 输入 `v0.2.0`，选择创建这个新标签。
3. **Target** 选择刚上传源码的默认分支。此后本次发布前先不要再改代码。
4. **Release title** 填：`PTA Code Typer v0.2.0 — macOS Pre-release`。
5. **Describe this release**：复制源码包里的 `docs/RELEASE_NOTES_v0.2.0.md` 全文。
6. 在附件区域上传以下三个文件：
   - `PTA-Code-Typer-v0.2.0-macOS-arm64-prerelease.zip`
   - `PTA-Code-Typer-v0.2.0-source.zip`
   - `SHA256SUMS.txt`
7. 勾选 **This is a pre-release**。这表示它是公开测试版本。
8. 可以先点 **Save draft** 检查；确认无误后点 **Publish release**。

`v0.2.0` 标签标记本次源码版本，Pre-release 是该 Release 的测试状态。GitHub 额外生成的 `Source code (zip)` 和 `Source code (tar.gz)` 也是源码，不是可直接运行的 App。
发布后用户在 Releases 下下载名字含 `macOS-arm64-prerelease` 的附件。

官方说明：https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository

## 5. 发布后亲自检查

1. 打开自己的仓库页，进入 Releases。
2. 下载发布附件到一个新文件夹，确认可以完整解压。
3. 在该文件夹中打开终端，可运行 `shasum -a 256 -c SHA256SUMS.txt`，两个 ZIP 应显示 `OK`。
4. 按 App 包内 `安装说明.md` 测试首次打开、辅助功能授权和代码输入，不提交测试答案。
5. 把 Release 链接发给使用者。未来反馈可集中在仓库 **Issues**。

没有付费 Apple Developer 账户时可通过 GitHub 分发，但系统首次打开可能拦截。
“仍要打开”由使用者在确认来源后自行操作；本包不会自动更改系统安全设置。
Apple 官方说明：https://support.apple.com/zh-cn/102445

## 6. 以后更新

先修改与测试源码，提交至默认分支，再构建新 App，为新版本建立新标签（例如 `v0.2.0`），
发布对应新 ZIP 与校验文件。不要把不同代码重复标为同一个已发布版本。
当前 App 没有自动更新，用户需要下载新版本。临时签名变化时可能需要重新关联辅助功能权限。

第一次流程熟悉后，可改用 GitHub Desktop 管理提交；再往后可用 GitHub Actions 自动构建。
