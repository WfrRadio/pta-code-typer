#!/bin/zsh
APP_ROOT="${0:A:h}"
if [[ -d "$APP_ROOT/dist/PTA Code Typer.app" ]]; then
  /usr/bin/open "$APP_ROOT/dist/PTA Code Typer.app"
else
  /usr/bin/osascript -e 'display alert "请先构建原生 App" message "源码版需要 macOS 26+ SDK、Swift 编译器和 Python 构建环境，请按 readme.md 的命令构建。下载发布版可直接打开 .app，无需安装这些工具。"'
fi
