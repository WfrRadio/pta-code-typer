#!/usr/bin/env python3
"""Local JSON-lines bridge for the native SwiftUI application."""
import json
from pathlib import Path
import platform
import sys
import threading

from pta_typer import __version__
from pta_typer.core import Cancelled, Control, Options, normalize_code, run_typing
from pta_typer.backends.macos import MacKeyboard, permission_status


def reports_directory():
    return Path.home() / 'Library/Application Support/PTA Code Typer/reports'


class EngineServer:
    def __init__(self, output=None, keyboard_factory=MacKeyboard, runner=run_typing):
        self.output = sys.stdout if output is None else output
        self.keyboard_factory, self.runner = keyboard_factory, runner
        self.output_lock = threading.Lock()
        self.worker = None
        self.control = None

    def emit(self, event, value=None):
        with self.output_lock:
            self.output.write(json.dumps({'event': event, 'value': value}, ensure_ascii=False) + '\n')
            self.output.flush()

    def handle(self, request):
        if not isinstance(request, dict):
            raise ValueError('输入组件请求必须是 JSON 对象。')
        command = request.get('command')
        if command == 'environment':
            self.emit('environment', {'accessibility': permission_status(), 'python': platform.python_version(),
                                      'version': __version__, 'reports': str(reports_directory())})
        elif command == 'cancel':
            if self.control is not None:
                self.control.event.set()
        elif command == 'start':
            if self.worker is not None and self.worker.is_alive():
                raise ValueError('已有输入任务正在运行，请先停止。')
            source = request.get('code')
            if not isinstance(source, str) or len(source.encode('utf-8')) > 256 * 1024:
                raise ValueError('请输入不超过 256 KB 的代码文本。')
            if not normalize_code(source).strip():
                raise ValueError('请先读取剪贴板或导入代码。')
            options = Options(**request.get('options', {}))
            options.validate()
            directory = reports_directory()
            directory.mkdir(parents=True, exist_ok=True)
            self.control = Control()
            self.worker = threading.Thread(target=self.work, args=(source, options, directory), daemon=True)
            self.worker.start()
        else:
            raise ValueError('不支持的输入组件命令。')

    def work(self, source, options, directory):
        keyboard = None
        try:
            keyboard = self.keyboard_factory(self.control)
            result = self.runner(keyboard, source, options, directory, emit=self.emit)
            self.emit('done', result)
        except Cancelled as error:
            self.emit('cancelled', str(error))
        except Exception as error:
            self.emit('error', str(error))
        finally:
            try:
                if keyboard is not None:
                    keyboard.close()
            finally:
                self.emit('finished')

    def close(self):
        if self.control is not None:
            self.control.event.set()
        if self.worker is not None:
            self.worker.join(timeout=15)


def main():
    server = EngineServer()
    try:
        for line in sys.stdin:
            request = None
            try:
                request = json.loads(line)
                server.handle(request)
            except Exception as error:
                server.emit('error', str(error))
                if (isinstance(request, dict) and request.get('command') == 'start' and
                        (server.worker is None or not server.worker.is_alive())):
                    server.emit('finished')
    finally:
        server.close()


if __name__ == '__main__':
    main()
