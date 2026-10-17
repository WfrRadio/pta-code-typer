"""macOS native Unicode input and accessibility checks."""
import ctypes as C
import subprocess
import time
from pathlib import Path
from ..core import ClipboardReader, Cancelled, Stopped, is_pta_url

CATALOG = {
    'edge': ('Microsoft Edge', 'com.microsoft.edgemac', 'Microsoft Edge.app'),
    'chrome': ('Google Chrome', 'com.google.Chrome', 'Google Chrome.app'),
    'brave': ('Brave', 'com.brave.Browser', 'Brave Browser.app'),
    'chromium': ('Chromium', 'org.chromium.Chromium', 'Chromium.app'),
}


def permission_status():
    ax = C.CDLL('/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices')
    ax.AXIsProcessTrusted.restype = C.c_bool
    ax.AXIsProcessTrusted.argtypes = []
    return bool(ax.AXIsProcessTrusted())


def request_permission():
    """Only asks macOS to display its permission prompt; never grants access."""
    cf = C.CDLL('/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation')
    ax = C.CDLL('/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices')
    cf.CFDictionaryCreateMutable.restype = C.c_void_p
    cf.CFDictionaryCreateMutable.argtypes = [C.c_void_p, C.c_long, C.c_void_p, C.c_void_p]
    cf.CFDictionarySetValue.argtypes = [C.c_void_p, C.c_void_p, C.c_void_p]
    cf.CFRelease.argtypes = [C.c_void_p]
    ax.AXIsProcessTrustedWithOptions.restype = C.c_bool
    ax.AXIsProcessTrustedWithOptions.argtypes = [C.c_void_p]
    options = cf.CFDictionaryCreateMutable(None, 0, None, None)
    try:
        key = C.c_void_p.in_dll(ax, 'kAXTrustedCheckOptionPrompt')
        value = C.c_void_p.in_dll(cf, 'kCFBooleanTrue')
        cf.CFDictionarySetValue(options, key, value)
        return bool(ax.AXIsProcessTrustedWithOptions(options))
    finally:
        cf.CFRelease(options)


def clipboard_get():
    return subprocess.run(['/usr/bin/pbpaste'], capture_output=True, check=True,
                          timeout=5, encoding='utf-8', text=True).stdout


def clipboard_set(text):
    subprocess.run(['/usr/bin/pbcopy'], input=text.encode('utf-8'), check=True, timeout=5)


class MacKeyboard(ClipboardReader):
    COMMAND, SHIFT = 1 << 20, 1 << 17
    LEFT, RIGHT, DOWN = 123, 124, 125
    RETURN, ESCAPE, BACKSPACE, FORWARD_DELETE = 36, 53, 51, 117

    def __init__(self, control, *, test_url=None):
        if not permission_status():
            raise Stopped('尚未获得辅助功能权限。请在应用的“环境与权限”中完成授权，再重新检测。')
        self.control, self.test_url = control, test_url
        self.cf = C.CDLL('/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation')
        self.cg = C.CDLL('/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics')
        self.ax = C.CDLL('/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices')
        self.proc = C.CDLL('/usr/lib/libproc.dylib')
        C.CDLL('/System/Library/Frameworks/AppKit.framework/AppKit')
        self.objc = C.CDLL('/usr/lib/libobjc.A.dylib')
        self._bind(self.objc, 'objc_getClass', C.c_void_p, C.c_char_p)
        self._bind(self.objc, 'sel_registerName', C.c_void_p, C.c_char_p)
        self.message = C.CFUNCTYPE(C.c_void_p, C.c_void_p, C.c_void_p)(('objc_msgSend', self.objc))
        self.pid_message = C.CFUNCTYPE(C.c_int, C.c_void_p, C.c_void_p)(('objc_msgSend', self.objc))
        self.pool = self.message(self.objc.objc_getClass(b'NSAutoreleasePool'), self.selector('new'))
        self.workspace = self.message(self.objc.objc_getClass(b'NSWorkspace'), self.selector('sharedWorkspace'))
        self._bind(self.cf, 'CFRelease', None, C.c_void_p)
        self._bind(self.cf, 'CFEqual', C.c_bool, C.c_void_p, C.c_void_p)
        self._bind(self.cf, 'CFStringGetLength', C.c_long, C.c_void_p)
        self._bind(self.cf, 'CFStringCreateWithCString', C.c_void_p, C.c_void_p, C.c_char_p, C.c_uint32)
        self._bind(self.cf, 'CFStringGetCString', C.c_bool, C.c_void_p, C.c_char_p, C.c_long, C.c_uint32)
        self._bind(self.ax, 'AXUIElementCreateApplication', C.c_void_p, C.c_int)
        self._bind(self.ax, 'AXUIElementCopyAttributeValue', C.c_int, C.c_void_p, C.c_void_p, C.POINTER(C.c_void_p))
        self._bind(self.ax, 'AXUIElementIsAttributeSettable', C.c_int, C.c_void_p, C.c_void_p, C.POINTER(C.c_bool))
        self._bind(self.cg, 'CGEventCreateKeyboardEvent', C.c_void_p, C.c_void_p, C.c_uint16, C.c_bool)
        self._bind(self.cg, 'CGEventKeyboardSetUnicodeString', None, C.c_void_p, C.c_ulong, C.POINTER(C.c_uint16))
        self._bind(self.cg, 'CGEventSetFlags', None, C.c_void_p, C.c_uint64)
        self._bind(self.cg, 'CGEventPost', None, C.c_uint32, C.c_void_p)
        self._bind(self.cg, 'CGEventCreate', C.c_void_p, C.c_void_p)
        self._bind(self.cg, 'CGEventSourceKeyState', C.c_bool, C.c_int, C.c_uint16)
        class Point(C.Structure):
            _fields_ = [('x', C.c_double), ('y', C.c_double)]
        self._bind(self.cg, 'CGEventGetLocation', Point, C.c_void_p)
        self._bind(self.proc, 'proc_pidpath', C.c_int, C.c_int, C.c_void_p, C.c_uint32)
        self.application, self.editor, self.target_pid = None, None, None
        self.attributes = {name: self.string(name) for name in ('AXFocusedUIElement', 'AXRole', 'AXValue')}
        self.target_url, self.browser_name = '', ''

    @staticmethod
    def _bind(lib, name, result, *arguments):
        function = getattr(lib, name)
        function.restype, function.argtypes = result, arguments

    def selector(self, name):
        return self.objc.sel_registerName(name.encode())

    def string(self, text):
        return self.cf.CFStringCreateWithCString(None, text.encode(), 0x08000100)

    def front_pid(self):
        app = self.message(self.workspace, self.selector('frontmostApplication'))
        if not app:
            raise Stopped('无法确认前台应用，已停止。')
        return self.pid_message(app, self.selector('processIdentifier'))

    def attribute(self, element, name):
        result = C.c_void_p()
        if self.ax.AXUIElementCopyAttributeValue(element, self.attributes[name], C.byref(result)) or not result.value:
            raise Stopped('无法读取代码框焦点，请直接点击代码正文区域后重试。')
        return result

    def cf_text(self, value):
        buffer = C.create_string_buffer(self.cf.CFStringGetLength(value) * 4 + 1)
        if not self.cf.CFStringGetCString(value, buffer, len(buffer), 0x08000100):
            raise Stopped('无法读取辅助功能文本。')
        return buffer.value.decode('utf-8')

    def lock_target(self, selected='auto'):
        self.control.check()
        pid = self.front_pid()
        path = C.create_string_buffer(4096)
        self.proc.proc_pidpath(pid, path, len(path))
        found = next(((key, item) for key, item in CATALOG.items() if (item[2] + '/').encode() in path.value), None)
        if found is None:
            raise Stopped('前台不是支持的浏览器。请在倒计时内点击 Edge、Chrome、Brave 或 Chromium 的 PTA 代码框。')
        key, (label, bundle, _) = found
        if selected not in ('auto', key):
            raise Stopped(f'当前前台是 {label}，与选定的浏览器不符。')
        self.browser_name = label
        script = f'tell application id "{bundle}" to get URL of active tab of front window'
        try:
            url = subprocess.run(['/usr/bin/osascript', '-e', script], capture_output=True,
                                 text=True, timeout=12, check=True).stdout.strip()
        except subprocess.CalledProcessError as error:
            raise Stopped('无法读取浏览器标签页。请允许应用自动化控制所选浏览器，再重试。') from error
        except subprocess.TimeoutExpired as error:
            raise Stopped('读取标签页超时；如系统正在询问自动化权限，请完成授权后重试。') from error
        # test_url is only passed by the local regression runner, never by the GUI.
        if not is_pta_url(url) and not (self.test_url and url == self.test_url):
            raise Stopped('当前标签页不是 pintia.cn，未执行清空或输入。')
        self.target_url, self.target_pid = url, pid
        self.application = self.ax.AXUIElementCreateApplication(pid)
        self.editor = self.attribute(self.application, 'AXFocusedUIElement')
        role = self.attribute(self.editor, 'AXRole')
        try:
            if self.cf_text(role) != 'AXTextArea':
                raise Stopped('焦点不是多行代码框，请点击 PTA 代码正文区域后重试。')
        finally:
            self.cf.CFRelease(role)
        editable = C.c_bool()
        error = self.ax.AXUIElementIsAttributeSettable(self.editor, self.attributes['AXValue'], C.byref(editable))
        if error or not editable.value:
            raise Stopped('当前区域不可编辑，请点击作答代码框，而不是题目中的示例。')
        self.check()

    def editor_empty(self):
        try:
            value = self.attribute(self.editor, 'AXValue')
            try:
                return self.cf.CFStringGetLength(value) == 0
            finally:
                self.cf.CFRelease(value)
        except Stopped:
            return False

    def check(self):
        self.control.check()
        if self.cg.CGEventSourceKeyState(1, 100):
            raise Cancelled('检测到 F8，已停止输入。')
        if self.target_pid is not None:
            if self.front_pid() != self.target_pid:
                raise Stopped('前台应用发生切换，已停止输入。')
            focused = self.attribute(self.application, 'AXFocusedUIElement')
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
                raise Cancelled('鼠标已移至屏幕左上角，输入已停止。')
        finally:
            self.cf.CFRelease(event)

    def _post(self, code, down, flags, text):
        event = self.cg.CGEventCreateKeyboardEvent(None, code, down)
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

    def key(self, code, flags=0, text=None):
        self.check()
        self._post(code, True, flags, text)
        try:
            time.sleep(.004)
        finally:
            self._post(code, False, flags, text)
        self.control.wait(.004)

    def text(self, ch): self.key(0, text=ch)
    def select_all(self): self.key(0, self.COMMAND)
    def copy(self): self.key(8, self.COMMAND)
    def escape(self): self.key(self.ESCAPE)
    def backspace(self): self.key(self.BACKSPACE)
    def delete_forward(self): self.key(self.FORWARD_DELETE)
    def enter(self): self.key(self.RETURN)
    def line_start(self): self.key(self.LEFT, self.COMMAND)
    def line_end(self): self.key(self.RIGHT, self.COMMAND)
    def right(self): self.key(self.RIGHT)
    def clipboard_get(self): return clipboard_get()
    def clipboard_set(self, text): clipboard_set(text)

    def trim_tail(self):
        self.escape()
        self.key(self.DOWN, self.COMMAND | self.SHIFT)
        self.delete_forward()

    def clear_autoindent(self):
        self.trim_tail()
        self.key(self.LEFT, self.COMMAND | self.SHIFT)
        self.delete_forward()

    def close(self):
        for value in (self.editor, self.application, *self.attributes.values()):
            if value:
                self.cf.CFRelease(value)
        self.message(self.pool, self.selector('drain'))
