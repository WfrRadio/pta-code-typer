#!/bin/zsh
APP_ROOT="${0:A:h}"
APP_PYTHON="$APP_ROOT/.venv/bin/python"
if [[ ! -x "$APP_PYTHON" ]]; then
  APP_PYTHON="$(command -v python3)"
fi
if [[ -z "$APP_PYTHON" ]] || ! "$APP_PYTHON" -B -c 'import tkinter; tkinter.Tcl()' >/dev/null 2>&1; then
  /usr/bin/osascript -e 'display alert "缺少 Python 图形环境" message "请优先打开 dist 中的 .app，内置环境无需配置。源码运行需要 Python 3.11+ 和可用的 tkinter，详见 readme.md。"'
  exit 1
fi
cd "$APP_ROOT" || exit 1
exec "$APP_PYTHON" -B "$APP_ROOT/main.py"
