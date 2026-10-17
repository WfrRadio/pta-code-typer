"""Platform-independent typing, cancellation, verification and local reports."""
from dataclasses import dataclass
from datetime import datetime
import hashlib
import json
from pathlib import Path
import re
import sys
import threading
import time
from urllib.parse import urlsplit
import uuid


class Stopped(RuntimeError):
    """Input must stop; never retry a focus or permission error."""


class Cancelled(Stopped):
    pass


class Mismatch(Stopped):
    def __init__(self, expected, actual):
        self.expected, self.actual = expected, actual
        self.position = next((i for i, (a, b) in enumerate(zip(expected, actual)) if a != b), min(len(expected), len(actual)))
        self.line = expected[:self.position].count('\n') + 1
        self.column = self.position - expected.rfind('\n', 0, self.position)
        super().__init__(f'第 {self.line} 行第 {self.column} 列不一致：预期 {expected[self.position:self.position+1]!r}，实际 {actual[self.position:self.position+1]!r}')


def normalize_code(text):
    text = text.removeprefix('\ufeff').replace('\r\n', '\n').replace('\r', '\n')
    lines = []
    for line in text.split('\n'):
        n = len(line) - len(line.lstrip(' \t\u00a0\u202f'))
        indent = line[:n].replace('\u00a0', ' ').replace('\u202f', ' ').expandtabs(4)
        lines.append(indent + line[n:])
    result = '\n'.join(lines)
    if any((ord(ch) < 32 and ch not in '\n\t') or ord(ch) == 127 for ch in result):
        raise ValueError('代码含控制字符；请移除 NUL、Escape 等不可输入字符。')
    if any(0xD800 <= ord(ch) <= 0xDFFF for ch in result):
        raise ValueError('代码含无效 Unicode 字符，请重新以 UTF-8 导入。')
    return result


def is_pta_url(url):
    parsed = urlsplit(url)
    host = (parsed.hostname or '').lower()
    return parsed.scheme in ('http', 'https') and (host == 'pintia.cn' or host.endswith('.pintia.cn'))


def task_warning(url, code):
    if '/problems/type/6' in urlsplit(url).path and re.search(r'(?m)^\s*(?:int|void|auto)\s+main\s*\(', code):
        return '当前是函数题，平台通常已提供 main()。完整程序应输入对应的编程题；输入校验通过不代表当前题目可以编译。'
    return ''


def app_home():
    if getattr(sys, 'frozen', False):
        executable = Path(sys.executable).resolve()
        for parent in executable.parents:
            if parent.suffix == '.app':
                return parent.parent
        return executable.parent
    return Path(__file__).resolve().parent.parent


@dataclass(frozen=True)
class Options:
    delay: float = .045
    countdown: int = 7
    check_every: int = 1
    retries: int = 1
    browser: str = 'auto'

    def validate(self):
        if not .02 <= self.delay <= 1:
            raise ValueError('字符间隔必须为 0.02～1 秒。')
        if not 3 <= self.countdown <= 30:
            raise ValueError('倒计时必须为 3～30 秒。')
        if not 1 <= self.check_every <= 20 or not 0 <= self.retries <= 2:
            raise ValueError('校验间隔或重试次数无效。')


class Control:
    def __init__(self, event=None):
        self.event = event or threading.Event()

    def check(self):
        if self.event.is_set():
            raise Cancelled('已停止输入。编辑框中可能只有部分代码，请检查后再操作。')

    def wait(self, seconds):
        if self.event.wait(max(0, seconds)):
            self.check()


class ClipboardReader:
    """Read twice, preserve clipboard text, and never use paste for input."""
    def read_editor(self):
        original = self.clipboard_get()
        marker = 'PTA_CHECK_' + uuid.uuid4().hex
        try:
            for _ in range(3):
                self.clipboard_set(marker)
                self.check()
                self.select_all()
                self.copy()
                previous = None
                for _ in range(5):
                    self.control.wait(.1)
                    actual = self.clipboard_get()
                    if actual != marker and actual == previous:
                        self.check()
                        self.right()
                        return actual.replace('\r\n', '\n').replace('\r', '\n')
                    previous = actual
            raise Stopped('复制未响应或剪贴板不稳定，无法校验。请暂停其他复制操作后重试。')
        finally:
            self.clipboard_set(original)


def write_report(directory, code, actual, status, **extra):
    folder = Path(directory) / datetime.now().strftime('%Y%m%d-%H%M%S-%f')
    folder.mkdir(parents=True, exist_ok=False)
    (folder / 'expected.txt').write_text(code, encoding='utf-8')
    if actual is not None:
        (folder / 'actual.txt').write_text(actual, encoding='utf-8')
    summary = {'status': status, 'expected_chars': len(code),
               'actual_chars': len(actual) if actual is not None else None,
               'expected_sha256': hashlib.sha256(code.encode()).hexdigest(),
               'actual_sha256': hashlib.sha256(actual.encode()).hexdigest() if actual is not None else None,
               **extra}
    if actual is not None and actual != code:
        error = Mismatch(code, actual)
        summary.update(line=error.line, column=error.column, difference=str(error))
    (folder / 'report.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
    return folder


def type_once(keyboard, code, options, delay, emit):
    keyboard.check()
    keyboard.escape()
    keyboard.select_all()
    keyboard.backspace()
    keyboard.control.wait(.25)
    lines = code.split('\n')
    prefix = ''
    for index, line in enumerate(lines):
        keyboard.check()
        indent_size = len(line) - len(line.lstrip(' '))
        indent, content = line[:indent_size], line[indent_size:]
        # Temporary non-whitespace guard prevents } from re-indenting the line.
        keyboard.text('x')
        keyboard.control.wait(delay)
        keyboard.trim_tail()
        for character in content:
            keyboard.check()
            keyboard.text(character)
            keyboard.control.wait(delay)
            keyboard.trim_tail()
        keyboard.line_start()
        keyboard.delete_forward()
        for character in indent:
            keyboard.text(character)
            keyboard.control.wait(delay)
        keyboard.line_end()
        prefix += ('\n' if index else '') + line
        if (index + 1) % options.check_every == 0 or index == len(lines) - 1:
            actual = keyboard.read_editor()
            if actual != prefix:
                actual = keyboard.read_editor()
            if actual != prefix:
                raise Mismatch(prefix, actual)
        emit('progress', {'line': index + 1, 'total': len(lines)})
        if index < len(lines) - 1:
            keyboard.enter()
            keyboard.control.wait(max(delay, .08))
            keyboard.clear_autoindent()
    return actual


def run_typing(keyboard, source, options, directory, emit=lambda *_: None, countdown=True):
    options.validate()
    code = normalize_code(source)
    if not code.strip():
        raise ValueError('请先读取剪贴板或导入非空代码文件。')
    if countdown:
        deadline = time.monotonic() + options.countdown
        while True:
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                break
            emit('countdown', int(remaining) + 1)
            keyboard.control.wait(min(1, remaining))
    keyboard.lock_target(options.browser)
    emit('target', {'browser': keyboard.browser_name, 'url': keyboard.target_url})
    warning = task_warning(keyboard.target_url, code)
    if warning:
        emit('warning', warning)
    # Backup is taken before the first destructive key. Empty editors need no copy.
    if not keyboard.editor_empty():
        backup = keyboard.read_editor()
        backup_path = write_report(directory, code, backup, 'before-input') / 'actual.txt'
        emit('backup', str(backup_path))
    for attempt in range(options.retries + 1):
        keyboard.check()
        delay = min(options.delay * (1.7 ** attempt), 1)
        emit('attempt', {'attempt': attempt + 1, 'delay': delay})
        try:
            actual = type_once(keyboard, code, options, delay, emit)
        except Mismatch as error:
            report = write_report(directory, code, error.actual, 'mismatch',
                                  checked_prefix=error.expected, checked_line=error.line,
                                  checked_column=error.column, delay=delay, attempt=attempt + 1)
            emit('report', str(report))
            if attempt == options.retries:
                raise
            emit('warning', f'{error}；将降速重新输入。')
            continue
        report = write_report(directory, code, actual, 'verified', delay=delay, attempt=attempt + 1)
        emit('report', str(report))
        return {'characters': len(code), 'lines': len(code.split('\n')), 'report': str(report), 'warning': warning}
