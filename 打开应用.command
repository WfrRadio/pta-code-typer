#!/bin/zsh
APP_ROOT="${0:A:h}"
if [[ -d "$APP_ROOT/dist/PTA Code Typer.app" ]]; then
  /usr/bin/open "$APP_ROOT/dist/PTA Code Typer.app"
else
  /usr/bin/osascript -e 'display alert "尚未构建应用" message "请按 readme.md 构建，或运行“源码启动.command”。"'
fi
