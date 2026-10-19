#!/usr/bin/env python3
"""在 macOS Edge 或 Safari 的 PTA 编辑框中逐字输入剪贴板代码，不使用粘贴。

运行：python3 ~/pta.py
可选：python3 ~/pta.py --file source.c --delay 0.06
运行后有 5 秒时间点击代码框；鼠标移至屏幕左上角可中止。
仅使用 Python 标准库和 macOS 系统框架，无需 pip 包。
"""
import argparse
import ctypes as C
from datetime import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time
import uuid
from urllib.parse import urlsplit


class Stopped(RuntimeError):
    pass


class ProcessSerialNumber(C.Structure):
    _fields_ = [('high', C.c_uint32), ('low', C.c_uint32)]


SCRIPT_BROWSERS = {
    'edge': ('Microsoft Edge', 'com.microsoft.edgemac', 'active tab'),
    'safari': ('Safari', 'com.apple.Safari', 'current tab'),
}


class VerificationMismatch(Stopped):
    def __init__(self, expected, actual):
        self.expected, self.actual = expected, actual
        position = next((i for i, pair in enumerate(zip(expected, actual))
                         if pair[0] != pair[1]), min(len(expected), len(actual)))
        self.line = expected[:position].count('\n') + 1
        self.column = position - expected.rfind('\n', 0, position)
        want, got = expected[position:position + 1], actual[position:position + 1]
        super().__init__(f'回读不一致：第 {self.line} 行第 {self.column} 列，'
                         f'预期 {want!r}，实际 {got!r}（空字符串表示文本已结束）。')


def save_report(expected, actual, status, delay, attempt):
    folder = Path(__file__).resolve().parent / 'pta-input-reports'
    folder = folder / datetime.now().strftime('%Y%m%d-%H%M%S-%f')
    folder.mkdir(parents=True, exist_ok=False)
    (folder / 'expected.txt').write_text(expected, encoding='utf-8')
    if actual is not None:
        (folder / 'actual.txt').write_text(actual, encoding='utf-8')
    info = {'status': status, 'attempt': attempt, 'delay': delay,
            'expected_chars': len(expected),
            'actual_chars': len(actual) if actual is not None else None,
            'expected_sha256': hashlib.sha256(expected.encode()).hexdigest(),
            'actual_sha256': hashlib.sha256(actual.encode()).hexdigest() if actual is not None else None}
    if actual is not None and actual != expected:
        error = VerificationMismatch(expected, actual)
        info.update(line=error.line, column=error.column, difference=str(error))
    (folder / 'report.json').write_text(json.dumps(info, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f'\n校验记录：{folder}', flush=True)
    return folder


def normalize_code(text):
    text = text.removeprefix('\ufeff').replace('\r\n', '\n').replace('\r', '\n')
    result = []
    for line in text.split('\n'):
        # 仅修正网页复制的行首不换行空格，保留字符串、注释内的原文。
        count = len(line) - len(line.lstrip(' \t\u00a0\u202f'))
        indent = line[:count].replace('\u00a0', ' ').replace('\u202f', ' ')
        result.append(indent.expandtabs(4) + line[count:])
    text = '\n'.join(result)
    if any((ord(ch) < 32 and ch not in '\n\t') or ord(ch) == 127 for ch in text):
        raise ValueError('代码含不可输入的控制字符，请检查剪贴板。')
    return text


class MacKeyboard:
    COMMAND, SHIFT = 1 << 20, 1 << 17
    LEFT, RIGHT, DOWN = 123, 124, 125
    RETURN, ESCAPE, BACKSPACE, FORWARD_DELETE = 36, 53, 51, 117

    def __init__(self):
        self.cf = C.CDLL('/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation')
        self.cg = C.CDLL('/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics')
        self.ax = C.CDLL('/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices')
        C.CDLL('/System/Library/Frameworks/AppKit.framework/AppKit')
        self.objc = C.CDLL('/usr/lib/libobjc.A.dylib')
        self._bind(self.objc, 'objc_getClass', C.c_void_p, C.c_char_p)
        self._bind(self.objc, 'sel_registerName', C.c_void_p, C.c_char_p)
        self.message = C.CFUNCTYPE(C.c_void_p, C.c_void_p, C.c_void_p)(('objc_msgSend', self.objc))
        self.application_for_pid = C.CFUNCTYPE(C.c_void_p, C.c_void_p, C.c_void_p, C.c_int)(('objc_msgSend', self.objc))
        self._bind(self.cf, 'CFRelease', None, C.c_void_p)
        self._bind(self.cf, 'CFEqual', C.c_bool, C.c_void_p, C.c_void_p)
        self._bind(self.cf, 'CFStringGetCString', C.c_bool, C.c_void_p, C.c_char_p, C.c_long, C.c_uint32)
        self._bind(self.cf, 'CFStringCreateWithCString', C.c_void_p, C.c_void_p, C.c_char_p, C.c_uint32)
        self._bind(self.ax, 'AXIsProcessTrusted', C.c_bool)
        self._bind(self.ax, 'GetFrontProcess', C.c_int16, C.POINTER(ProcessSerialNumber))
        self._bind(self.ax, 'GetProcessPID', C.c_int16, C.POINTER(ProcessSerialNumber), C.POINTER(C.c_int))
        self._bind(self.ax, 'AXUIElementCreateApplication', C.c_void_p, C.c_int)
        self._bind(self.ax, 'AXUIElementCopyAttributeValue', C.c_int, C.c_void_p, C.c_void_p, C.POINTER(C.c_void_p))
        self._bind(self.ax, 'AXUIElementGetPid', C.c_int, C.c_void_p, C.POINTER(C.c_int))
        self._bind(self.cg, 'CGEventCreateKeyboardEvent', C.c_void_p, C.c_void_p, C.c_uint16, C.c_bool)
        self._bind(self.cg, 'CGEventKeyboardSetUnicodeString', None, C.c_void_p, C.c_ulong, C.POINTER(C.c_uint16))
        self._bind(self.cg, 'CGEventSetFlags', None, C.c_void_p, C.c_uint64)
        self._bind(self.cg, 'CGEventPost', None, C.c_uint32, C.c_void_p)
        self._bind(self.cg, 'CGEventCreate', C.c_void_p, C.c_void_p)
        class Point(C.Structure):
            _fields_ = [('x', C.c_double), ('y', C.c_double)]
        self._bind(self.cg, 'CGEventGetLocation', Point, C.c_void_p)
        if not self.ax.AXIsProcessTrusted():
            raise Stopped('缺少辅助功能权限：请在系统设置 → 隐私与安全性 → 辅助功能中允许运行本脚本的终端，然后重试。')
        self.application = None
        self.element_attr = self.cf.CFStringCreateWithCString(None, b'AXFocusedUIElement', 0x08000100)
        self.role_attr = self.cf.CFStringCreateWithCString(None, b'AXRole', 0x08000100)
        self.editor = None
        self.target_pid = None

    @staticmethod
    def _bind(lib, name, restype, *argtypes):
        fn = getattr(lib, name)
        fn.restype, fn.argtypes = restype, argtypes

    def front_pid(self):
        # NSWorkspace caches time-varying values until the main run loop runs.
        # This CLI has no Cocoa event loop. These legacy public Process Manager
        # APIs return the current front process synchronously on supported macOS.
        serial, pid = ProcessSerialNumber(), C.c_int()
        if (self.ax.GetFrontProcess(C.byref(serial)) or
                self.ax.GetProcessPID(C.byref(serial), C.byref(pid)) or pid.value <= 0):
            raise Stopped('无法确认前台应用，已停止输入。')
        return pid.value

    def bundle_for_pid(self, pid):
        app = self.application_for_pid(self.objc.objc_getClass(b'NSRunningApplication'),
                                       self.objc.sel_registerName(b'runningApplicationWithProcessIdentifier:'), pid)
        if not app:
            raise Stopped('前台进程已退出或无法识别，未执行输入。')
        bundle = self.message(app, self.objc.sel_registerName(b'bundleIdentifier'))
        buffer = C.create_string_buffer(1024)
        if not bundle or not self.cf.CFStringGetCString(bundle, buffer, len(buffer), 0x08000100):
            raise Stopped('无法读取前台应用标识，未执行输入。')
        return buffer.value.decode('utf-8')

    def lock_target(self, selected='auto'):
        pid = self.front_pid()
        bundle = self.bundle_for_pid(pid)
        found = next(((key, item) for key, item in SCRIPT_BROWSERS.items() if item[1] == bundle), None)
        if found is None:
            raise Stopped(f'前台不是 Edge 或 Safari（检测到 {bundle}，PID {pid}），未清空或输入任何内容。请重新运行并点击 PTA 编辑框。')
        key, (label, bundle, tab) = found
        if selected not in ('auto', key):
            raise Stopped(f'当前前台是 {label}，与选定浏览器不符，未执行输入。')
        self.browser_name = label
        # 只读取网址，不注入 JavaScript，不改变浏览器设置。
        url = subprocess.run(
            ['osascript', '-e', f'tell application id "{bundle}" to get URL of {tab} of front window'],
            capture_output=True, text=True, timeout=15, check=True,
        ).stdout.strip()
        host = urlsplit(url).hostname or ''
        if host != 'pintia.cn' and not host.endswith('.pintia.cn'):
            raise Stopped(f'当前 {label} 标签页不是 pintia.cn，已停止。')
        self.target_url = url
        self.target_pid = pid
        self.application = self.ax.AXUIElementCreateApplication(pid)
        self.editor = self.attribute(self.application, self.element_attr)
        role = self.attribute(self.editor, self.role_attr)
        try:
            buffer = C.create_string_buffer(128)
            if not self.cf.CFStringGetCString(role, buffer, len(buffer), 0x08000100) or buffer.value != b'AXTextArea':
                raise Stopped('焦点不在多行代码编辑框中，未执行输入。请点击 PTA 代码正文区域后重试。')
        finally:
            self.cf.CFRelease(role)
        self.check()

    def attribute(self, element, name):
        value = C.c_void_p()
        if self.ax.AXUIElementCopyAttributeValue(element, name, C.byref(value)) or not value.value:
            raise Stopped('无法读取编辑框焦点，已停止。')
        return value

    def check(self):
        if self.target_pid is not None and self.front_pid() != self.target_pid:
            raise Stopped('前台应用发生切换，已停止输入。')
        if self.editor is not None:
            focused = self.attribute(self.application, self.element_attr)
            try:
                if not self.cf.CFEqual(focused, self.editor):
                    raise Stopped('焦点已离开原代码框，已停止输入。')
            finally:
                self.cf.CFRelease(focused)
        event = self.cg.CGEventCreate(None)
        if not event:
            raise Stopped('无法读取鼠标位置。')
        try:
            point = self.cg.CGEventGetLocation(event)
            if point.x <= 3 and point.y <= 3:
                raise Stopped('鼠标已移至左上角，输入中止。')
        finally:
            self.cf.CFRelease(event)

    def key(self, keycode, flags=0, text=None):
        for down in (True, False):
            event = self.cg.CGEventCreateKeyboardEvent(None, keycode, down)
            if not event:
                raise Stopped('无法创建键盘事件。')
            try:
                self.cg.CGEventSetFlags(event, flags)
                if text is not None:
                    raw = text.encode('utf-16-le')
                    units = (C.c_uint16 * (len(raw) // 2)).from_buffer_copy(raw)
                    self.cg.CGEventKeyboardSetUnicodeString(event, len(units), units)
                self.cg.CGEventPost(0, event)
            finally:
                self.cf.CFRelease(event)
            time.sleep(0.004)

    def text(self, ch):
        # Unicode 文本事件不依赖 Shift+9/0 等物理组合键生成符号。
        self.key(0, text=ch)

    def trim_tail(self):
        # 正文始终在文档末尾追加；光标后方只可能是编辑器生成的内容。
        # 使用向前删除：无补全、空选区时在 EOF 删除是无操作。
        # 此处不能用 Backspace，否则会删掉刚输入的左括号。
        self.key(self.ESCAPE)
        self.key(self.DOWN, self.COMMAND | self.SHIFT)
        self.key(self.FORWARD_DELETE)

    def clear_autoindent(self):
        self.trim_tail()
        self.key(self.LEFT, self.COMMAND | self.SHIFT)
        self.key(self.FORWARD_DELETE)

    def read_text(self):
        # 此编辑器 AXValue 会把行首空格放到上一行末尾，不能用于精确校验。
        # 仅用复制回读（不是粘贴输入），并恢复剪贴板原文本。
        original = subprocess.run(['pbpaste'], capture_output=True, check=True, timeout=5).stdout
        marker = ('PTA_CHECK_' + uuid.uuid4().hex).encode()
        try:
            actual = marker
            for copy_attempt in range(3):
                subprocess.run(['pbcopy'], input=marker, check=True, timeout=5)
                self.check()
                self.key(0, self.COMMAND)
                self.key(8, self.COMMAND)  # Command+C
                previous = None
                for poll in range(5):
                    time.sleep(0.1)
                    actual = subprocess.run(['pbpaste'], capture_output=True, check=True, timeout=5).stdout
                    if actual != marker and actual == previous:
                        break
                    previous = actual
                else:
                    continue
                break
            else:
                raise Stopped('复制被禁止、未响应或剪贴板不稳定，无法校验；请暂停其他复制操作后重试。')
            self.check()
            self.key(self.RIGHT)
            return actual.decode('utf-8').replace('\r\n', '\n').replace('\r', '\n')
        finally:
            subprocess.run(['pbcopy'], input=original, check=True, timeout=5)


def _type_once(keyboard, code, delay, check_every):
    keyboard.key(keyboard.ESCAPE)
    keyboard.key(0, keyboard.COMMAND)  # Command+A：仅在已点击的代码框内全选。
    keyboard.key(keyboard.BACKSPACE)
    time.sleep(0.25)
    lines = code.split('\n')
    for index, line in enumerate(lines):
        keyboard.check()
        indent_size = len(line) - len(line.lstrip(' '))
        indent, content = line[:indent_size], line[indent_size:]
        # 临时行首字符阻止 } 触发编辑器自动重排缩进；本行写完后删除。
        # 单纯在第 0 列输入 } 仍会被 PTA 自动加上缩进，不能直接叠加源缩进。
        keyboard.text('x')
        time.sleep(delay)
        keyboard.trim_tail()
        for ch in content:
            keyboard.check()
            keyboard.text(ch)
            time.sleep(delay)
            keyboard.trim_tail()
        keyboard.key(keyboard.LEFT, keyboard.COMMAND)
        keyboard.key(keyboard.FORWARD_DELETE)  # 删除临时行首字符。
        for ch in indent:
            keyboard.check()
            keyboard.text(ch)
            time.sleep(delay)
        keyboard.key(keyboard.RIGHT, keyboard.COMMAND)
        # 每一批结束立即校验，错误不会积累到整篇输入完成才被发现。
        # 复制后光标回到文档末尾，不改变下一行的追加位置。
        if (index + 1) % check_every == 0 or index == len(lines) - 1:
            expected = '\n'.join(lines[:index + 1])
            actual = keyboard.read_text()
            if actual is None:
                raise Stopped('页面禁止复制，无法校验，已停止。当前只有部分代码，不能视为输入成功。')
            if actual != expected:
                # 剪贴板可能被跨设备同步或其他程序改写；先重新复制确认，
                # 避免把一次错误回读当作真实输入错误并无谓重输。
                actual = keyboard.read_text()
            if actual != expected:
                if actual is None:
                    raise Stopped('无法再次复制确认输入结果，已停止。')
                raise VerificationMismatch(expected, actual)
        if index < len(lines) - 1:
            keyboard.key(keyboard.RETURN)
            time.sleep(max(delay, 0.08))
            keyboard.clear_autoindent()
        print(f'\r已输入 {index + 1}/{len(lines)} 行', end='', flush=True)
    return actual


def type_code(keyboard, code, delay, check_every=1, retries=1):
    for attempt in range(retries + 1):
        keyboard.check()
        current_delay = min(delay * (1.7 ** attempt), 2)
        try:
            actual = _type_once(keyboard, code, current_delay, check_every)
        except VerificationMismatch as error:
            save_report(code, error.actual, 'mismatch', current_delay, attempt + 1)
            if attempt == retries:
                raise
            print(f'\n{error}\n将降低速度重新输入（第 {attempt + 2} 次）。', flush=True)
            continue
        save_report(code, actual, 'verified', current_delay, attempt + 1)
        unit = '每行' if check_every == 1 else f'每 {check_every} 行'
        print(f'\n输入完成，{unit}及全文均已回读校验一致。脚本不会提交答案。')
        return actual


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--file', type=Path, help='从 UTF-8 文件读取；默认读取剪贴板')
    parser.add_argument('--delay', type=float, default=0.045, help='每字符等待秒数，默认 0.045')
    parser.add_argument('--countdown', type=int, default=5, help='开始前等待秒数，默认 5')
    parser.add_argument('--check-every', type=int, default=1, help='每多少行校验一次，默认逐行校验')
    parser.add_argument('--retries', type=int, default=1, help='校验失败后降速重输的次数，默认 1')
    parser.add_argument('--browser', choices=['auto', 'edge', 'safari'], default='auto', help='默认自动识别 Edge 或 Safari')
    args = parser.parse_args()
    if sys.platform != 'darwin':
        parser.error('此脚本用于 macOS 的 Microsoft Edge 或 Safari。')
    if not 0.02 <= args.delay <= 2 or not 0 <= args.countdown <= 300:
        parser.error('--delay 必须在 0.02～2 秒之间，--countdown 必须在 0～300 秒之间。')
    if not 1 <= args.check_every <= 100 or not 0 <= args.retries <= 3:
        parser.error('--check-every 必须在 1～100 之间，--retries 必须在 0～3 之间。')
    try:
        source = (args.file.read_text(encoding='utf-8') if args.file else subprocess.run(
            ['pbpaste'], capture_output=True, text=True, check=True,
            encoding='utf-8', timeout=5, env={**os.environ, 'LANG': 'en_US.UTF-8'},
        ).stdout)
        code = normalize_code(source)
        if not code.strip():
            raise Stopped('剪贴板或代码文件为空，未执行输入。')
        keyboard = MacKeyboard()
        print(f'准备逐字输入 {len(code)} 个字符，共 {len(code.split(chr(10)))} 行。')
        print('请切换英文输入法，并点击 Edge 或 Safari 的 PTA 代码编辑框。原代码将被替换。')
        print('期间不要切换标签页、点击鼠标或移动光标；鼠标移至屏幕左上角可中止。')
        for remaining in range(args.countdown, 0, -1):
            print(f'{remaining} 秒后开始……', flush=True)
            time.sleep(1)
        keyboard.lock_target(args.browser)
        if '/problems/type/6' in urlsplit(keyboard.target_url).path and re.search(r'(?m)^\s*(?:int|void|auto)\s+main\s*\(', code):
            print('注意：当前是函数题，平台通常已提供 main()；完整程序应输入对应的编程题。输入校验通过不代表能在当前题目编译。', flush=True)
        type_code(keyboard, code, args.delay, args.check_every, args.retries)
        return 0
    except (Stopped, ValueError, OSError, subprocess.SubprocessError) as exc:
        print(f'\n已停止：{exc}', file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print('\n用户中止。', file=sys.stderr)
        return 130


if __name__ == '__main__':
    sys.exit(main())
