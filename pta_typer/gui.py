"""Small macOS desktop UI. Tk stays on the main thread; typing uses a worker."""
from datetime import datetime
from pathlib import Path
import platform
import queue
import subprocess
import sys
import threading
import tkinter as tk
from tkinter import filedialog, font, ttk
import traceback

from . import __version__
from .core import Cancelled, Control, Options, Stopped, app_home, normalize_code, run_typing
from .backends import BROWSERS, create_backend
from .backends.macos import clipboard_get, permission_status, request_permission

BG, CARD, INK, MUTED, BLUE = '#f4f6fa', '#ffffff', '#17243b', '#637189', '#2563eb'


class App:
    def __init__(self, root):
        self.root = root
        self.root.title('PTA Code Typer')
        self.root.geometry('1040x790')
        self.root.minsize(900, 690)
        self.root.configure(bg=BG)
        self.worker = None
        self.control = None
        self.events = queue.Queue()
        self.busy = False
        self.closing = False
        self.last_report = None
        self.source_name = '尚未载入代码'
        self.records = app_home() / 'data' / 'reports'
        self.style()
        self.build()
        self.refresh_environment()
        self.root.protocol('WM_DELETE_WINDOW', self.on_close)
        self.root.after(70, self.poll)
        self.root.bind('<Command-o>', lambda _: self.import_file())
        self.root.bind('<F8>', lambda _: self.stop())

    def style(self):
        style = ttk.Style(self.root)
        style.theme_use('clam')
        font.nametofont('TkDefaultFont').configure(size=13)
        font.nametofont('TkTextFont').configure(size=13)
        style.configure('.', font=('TkDefaultFont', 13), foreground=INK)
        style.configure('TFrame', background=BG)
        style.configure('Card.TFrame', background=CARD)
        style.configure('TLabel', background=BG, foreground=INK)
        style.configure('Card.TLabel', background=CARD)
        style.configure('Muted.TLabel', background=BG, foreground=MUTED)
        style.configure('CardMuted.TLabel', background=CARD, foreground=MUTED)
        style.configure('Title.TLabel', font=('TkDefaultFont', 26, 'bold'))
        style.configure('Heading.TLabel', font=('TkDefaultFont', 15, 'bold'), background=CARD)
        style.configure('TButton', padding=(14, 9), background='#e8edf5', borderwidth=0)
        style.map('TButton', background=[('active', '#dce5f3')])
        style.configure('Primary.TButton', padding=(22, 12), background=BLUE, foreground='white', font=('TkDefaultFont', 14, 'bold'))
        style.map('Primary.TButton', background=[('disabled', '#a5b6d4'), ('active', '#1d4ed8')], foreground=[('disabled', '#f8fafc')])
        style.configure('TNotebook', background=BG, borderwidth=0)
        style.configure('TNotebook.Tab', padding=(18, 11), background='#e8edf5')
        style.map('TNotebook.Tab', background=[('selected', CARD)], foreground=[('selected', BLUE)])
        style.configure('TProgressbar', troughcolor='#e7edf6', background=BLUE, borderwidth=0, thickness=8)
        style.configure('TCombobox', padding=5)

    def build(self):
        shell = ttk.Frame(self.root, padding=(28, 22, 28, 18))
        shell.pack(fill='both', expand=True)
        header = ttk.Frame(shell)
        header.pack(fill='x', pady=(0, 18))
        ttk.Label(header, text='PTA Code Typer', style='Title.TLabel').pack(anchor='w')
        ttk.Label(header, text='从剪贴板到代码框，逐字输入并核对结果。', style='Muted.TLabel').pack(anchor='w', pady=(5, 0))
        self.tabs = ttk.Notebook(shell)
        self.tabs.pack(fill='both', expand=True)
        self.input_tab = ttk.Frame(self.tabs, style='Card.TFrame', padding=20)
        self.environment_tab = ttk.Frame(self.tabs, style='Card.TFrame', padding=24)
        self.log_tab = ttk.Frame(self.tabs, style='Card.TFrame', padding=20)
        self.tabs.add(self.input_tab, text='1  代码与输入')
        self.tabs.add(self.environment_tab, text='2  环境与权限')
        self.tabs.add(self.log_tab, text='运行记录')
        self.build_input()
        self.build_environment()
        self.build_log()
        ttk.Label(shell, text=f'macOS · v{__version__}    仅本地处理 · 不自动提交答案', style='Muted.TLabel').pack(anchor='w', pady=(12, 0))

    def build_input(self):
        self.input_tab.columnconfigure(0, weight=1)
        self.input_tab.rowconfigure(2, weight=1)
        top = ttk.Frame(self.input_tab, style='Card.TFrame')
        top.grid(row=0, column=0, sticky='ew')
        ttk.Label(top, text='准备代码', style='Heading.TLabel').pack(side='left')
        self.clip_button = ttk.Button(top, text='读取剪贴板', command=self.load_clipboard)
        self.clip_button.pack(side='right')
        self.file_button = ttk.Button(top, text='导入文件…', command=self.import_file)
        self.file_button.pack(side='right', padx=(0, 9))
        self.source_label = ttk.Label(self.input_tab, text=self.source_name, style='CardMuted.TLabel')
        self.source_label.grid(row=1, column=0, sticky='w', pady=(10, 9))
        editor_frame = ttk.Frame(self.input_tab, style='Card.TFrame')
        editor_frame.grid(row=2, column=0, sticky='nsew')
        self.editor = tk.Text(editor_frame, wrap='none', font=('Menlo', 12), undo=True,
                              background='#f8faff', foreground=INK, insertbackground=BLUE,
                              selectbackground='#cfe0ff', relief='flat', borderwidth=0,
                              padx=12, pady=12, height=8, highlightthickness=1,
                              highlightbackground='#dce4ef', highlightcolor=BLUE)
        vertical = ttk.Scrollbar(editor_frame, orient='vertical', command=self.editor.yview)
        horizontal = ttk.Scrollbar(editor_frame, orient='horizontal', command=self.editor.xview)
        self.editor.configure(yscrollcommand=vertical.set, xscrollcommand=horizontal.set)
        self.editor.grid(row=0, column=0, sticky='nsew')
        vertical.grid(row=0, column=1, sticky='ns')
        horizontal.grid(row=1, column=0, sticky='ew')
        editor_frame.rowconfigure(0, weight=1)
        editor_frame.columnconfigure(0, weight=1)
        self.editor.bind('<<Modified>>', self.edited)
        controls = ttk.Frame(self.input_tab, style='Card.TFrame')
        controls.grid(row=3, column=0, sticky='ew', pady=(16, 9))
        self.browser = tk.StringVar(value=BROWSERS['auto'])
        self.speed = tk.StringVar(value='标准 · 45 ms')
        self.countdown = tk.StringVar(value='7')
        self.check = tk.StringVar(value='逐行核对')
        self.setting_widgets = []
        for column, (label, variable, values, width) in enumerate([
            ('浏览器', self.browser, list(BROWSERS.values()), 18),
            ('速度', self.speed, ['稳妥 · 70 ms', '标准 · 45 ms', '较快 · 25 ms'], 16),
            ('倒计时', self.countdown, ['5', '7', '10', '15', '20'], 7),
            ('校验', self.check, ['逐行核对', '每 5 行核对'], 13),
        ]):
            cell = ttk.Frame(controls, style='Card.TFrame')
            cell.grid(row=0, column=column, sticky='w', padx=(0, 18))
            ttk.Label(cell, text=label, style='CardMuted.TLabel').pack(anchor='w', pady=(0, 5))
            box = ttk.Combobox(cell, textvariable=variable, values=values, state='readonly', width=width)
            box.pack()
            self.setting_widgets.append(box)
        ttk.Label(self.input_tab, text='先切换英文输入法。点击开始后，在倒计时内点击 PTA 作答代码框；原内容会先备份再替换。',
                  style='CardMuted.TLabel', wraplength=840).grid(row=4, column=0, sticky='w', pady=(5, 12))
        action = ttk.Frame(self.input_tab, style='Card.TFrame')
        action.grid(row=5, column=0, sticky='ew')
        self.start_button = ttk.Button(action, text='开始输入', style='Primary.TButton', command=self.start)
        self.start_button.pack(side='left')
        self.stop_button = ttk.Button(action, text='停止', command=self.stop, state='disabled')
        self.stop_button.pack(side='left', padx=10)
        ttk.Label(action, text='紧急停止：F8 / Fn+F8，或鼠标移到屏幕左上角', style='CardMuted.TLabel').pack(side='left', padx=(8, 0))
        self.progress = ttk.Progressbar(self.input_tab, maximum=100)
        self.progress.grid(row=6, column=0, sticky='ew', pady=(14, 8))
        self.status = tk.StringVar(value='就绪：载入代码后，可先在“环境与权限”确认授权。')
        ttk.Label(self.input_tab, textvariable=self.status, style='Card.TLabel', wraplength=840).grid(row=7, column=0, sticky='w')

    def build_environment(self):
        page = self.environment_tab
        ttk.Label(page, text='运行环境', style='Heading.TLabel').pack(anchor='w')
        self.runtime_label = ttk.Label(page, text='', style='CardMuted.TLabel', wraplength=860)
        self.runtime_label.pack(anchor='w', pady=(10, 24))
        ttk.Label(page, text='允许应用输入代码', style='Heading.TLabel').pack(anchor='w')
        self.permission_label = ttk.Label(page, text='', style='Card.TLabel')
        self.permission_label.pack(anchor='w', pady=(10, 10))
        ttk.Label(page, text='1. 点击“打开辅助功能设置”。\n2. 在列表中开启 PTA Code Typer；如未列出，可用“＋”选择此应用。\n3. 返回应用，点击“重新检测”。\n\n从源码启动时，请授权实际启动它的终端。打包版应授权 PTA Code Typer 本身。',
                  style='CardMuted.TLabel', justify='left', wraplength=850).pack(anchor='w')
        row = ttk.Frame(page, style='Card.TFrame')
        row.pack(anchor='w', pady=(16, 24))
        self.permission_button = ttk.Button(row, text='打开辅助功能设置', command=self.open_permissions)
        self.permission_button.pack(side='left')
        self.refresh_button = ttk.Button(row, text='重新检测', command=self.refresh_environment)
        self.refresh_button.pack(side='left', padx=10)
        ttk.Label(page, text='首次识别浏览器', style='Heading.TLabel').pack(anchor='w')
        ttk.Label(page, text='macOS 可能询问是否允许本应用控制 Edge 或 Chrome，用于读取当前标签页网址。允许后再试即可。\n应用不会注入网页脚本，不需要屏幕录制权限，也不会提交答案。',
                  style='CardMuted.TLabel', wraplength=850, justify='left').pack(anchor='w', pady=(10, 22))
        ttk.Label(page, text='浏览器范围', style='Heading.TLabel').pack(anchor='w')
        ttk.Label(page, text='已加入 Edge、Chrome、Brave、Chromium 识别。具体兼容性以 README 的实测记录为准。\n仅向 pintia.cn 页面中可编辑的多行代码框输入；切换应用或焦点时会停止。',
                  style='CardMuted.TLabel', wraplength=850, justify='left').pack(anchor='w', pady=(10, 0))

    def build_log(self):
        row = ttk.Frame(self.log_tab, style='Card.TFrame')
        row.pack(fill='x', pady=(0, 12))
        ttk.Label(row, text='过程与校验结果', style='Heading.TLabel').pack(side='left')
        ttk.Button(row, text='打开记录文件夹', command=self.open_records).pack(side='right')
        self.log = tk.Text(self.log_tab, background='#f8faff', foreground=INK,
                           font=('Menlo', 11), relief='flat', wrap='word', padx=12, pady=12, state='disabled')
        self.log.pack(fill='both', expand=True)
        ttk.Label(self.log_tab, text='记录只保存在本机，可能包含输入代码和替换前的代码备份。开源时不要上传 data 文件夹。',
                  style='CardMuted.TLabel', wraplength=850).pack(anchor='w', pady=(12, 0))

    def edited(self, _=None):
        if self.editor.edit_modified():
            text = self.editor.get('1.0', 'end-1c')
            self.source_label.configure(text=f'{self.source_name} · {len(text):,} 字符 · {len(text.split(chr(10)))} 行')
            self.editor.edit_modified(False)

    def set_source(self, text, name):
        if self.busy:
            return
        self.source_name = name
        self.editor.delete('1.0', 'end')
        self.editor.insert('1.0', text)
        self.editor.edit_modified(True)
        self.edited()
        self.status.set('代码已载入，可预览或编辑。准备好后点击“开始输入”。')

    def load_clipboard(self):
        if self.busy:
            return
        try:
            text = clipboard_get()
            if not text.strip():
                raise ValueError('剪贴板中没有非空文本。')
            self.set_source(text, '来自剪贴板')
        except Exception as error:
            self.notify_error(error)

    def import_file(self):
        if self.busy:
            return
        path = filedialog.askopenfilename(parent=self.root, title='选择代码文件',
                                         filetypes=[('代码或文本', '*.c *.cpp *.cc *.h *.hpp *.py *.java *.txt'), ('所有文件', '*')])
        if not path:
            return
        try:
            source = Path(path)
            if source.stat().st_size > 256 * 1024:
                raise ValueError('文件超过 256 KB，请确认选择的是代码文件。')
            self.set_source(source.read_text(encoding='utf-8-sig'), f'文件：{source.name}')
        except UnicodeDecodeError:
            self.notify_error('文件不是 UTF-8，请在编辑器中另存为 UTF-8 后导入。')
        except Exception as error:
            self.notify_error(error)

    def refresh_environment(self):
        bundled = bool(getattr(sys, 'frozen', False))
        text = 'Python 与界面依赖已内置，无需另行安装。' if bundled else '正在使用独立 Python 环境；打包后的应用会内置 Python 与界面依赖。'
        self.runtime_label.configure(text=f'{text}\nPython {platform.python_version()} · Tk {self.root.tk.call("info", "patchlevel")} · {platform.machine()}')
        allowed = permission_status()
        self.permission_label.configure(text='已获得辅助功能权限，可以进行输入测试。' if allowed else '尚未获得辅助功能权限，输入功能暂不可用。',
                                        foreground='#15803d' if allowed else '#b45309')
        return allowed

    def open_permissions(self):
        if self.busy:
            return
        try:
            request_permission()
            subprocess.Popen(['/usr/bin/open', 'x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility'])
        except Exception as error:
            self.notify_error(error)

    def options(self):
        browser = next(key for key, value in BROWSERS.items() if value == self.browser.get())
        delay = {'稳妥 · 70 ms': .07, '标准 · 45 ms': .045, '较快 · 25 ms': .025}[self.speed.get()]
        return Options(delay=delay, countdown=int(self.countdown.get()), browser=browser,
                       check_every=1 if self.check.get() == '逐行核对' else 5)

    def start(self):
        if self.busy:
            return
        try:
            source = self.editor.get('1.0', 'end-1c')
            code = normalize_code(source)
            if not code.strip():
                raise ValueError('请先读取剪贴板或导入代码。')
            if len(code.encode('utf-8')) > 256 * 1024:
                raise ValueError('代码超过 256 KB，请缩小输入内容。')
            options = self.options()
            options.validate()
            if not self.refresh_environment():
                self.tabs.select(self.environment_tab)
                self.status.set('请先完成辅助功能授权，再重新开始。')
                return
            self.records.mkdir(parents=True, exist_ok=True)
            # Check writability before any editor content is replaced.
            probe = self.records / '.write-check'
            probe.write_text('ok')
            probe.unlink()
        except Exception as error:
            self.notify_error(error)
            return
        self.control = Control()
        self.set_busy(True)
        self.progress['value'] = 0
        self.tabs.select(self.input_tab)
        self.log_message('开始准备输入。请切换英文输入法，并在倒计时内点击 PTA 作答代码框。')
        self.worker = threading.Thread(target=self.work, args=(source, options), daemon=True)
        self.worker.start()
        self.root.after(250, self.minimize_for_target)

    def minimize_for_target(self):
        if self.busy:
            self.root.iconify()

    def work(self, source, options):
        keyboard = None
        try:
            keyboard = create_backend(self.control)
            result = run_typing(keyboard, source, options, self.records,
                                emit=lambda kind, value: self.events.put((kind, value)))
            self.events.put(('done', result))
        except Cancelled as error:
            self.events.put(('cancelled', str(error)))
        except Exception as error:
            self.events.put(('error', str(error)))
        finally:
            try:
                if keyboard is not None:
                    keyboard.close()
            finally:
                self.events.put(('finished', None))

    def set_busy(self, busy):
        self.busy = busy
        for widget in (self.start_button, self.file_button, self.clip_button, self.permission_button, self.refresh_button):
            widget.configure(state='disabled' if busy else 'normal')
        for widget in self.setting_widgets:
            widget.configure(state='disabled' if busy else 'readonly')
        self.editor.configure(state='disabled' if busy else 'normal')
        self.stop_button.configure(state='normal' if busy else 'disabled')

    def stop(self):
        if self.control is not None:
            self.control.event.set()
            self.status.set('正在安全停止…')

    def poll(self):
        try:
            while True:
                kind, value = self.events.get_nowait()
                if kind == 'countdown':
                    self.status.set(f'{value} 秒后开始：请点击浏览器中的 PTA 代码框。')
                elif kind == 'progress':
                    self.progress['value'] = value['line'] / value['total'] * 100
                    self.status.set(f"已输入 {value['line']}/{value['total']} 行，正在核对。")
                elif kind == 'target':
                    self.log_message(f"已锁定 {value['browser']} 中的 PTA 编辑框。")
                elif kind == 'attempt':
                    self.log_message(f"第 {value['attempt']} 次输入 · 字符间隔 {value['delay'] * 1000:.0f} ms")
                elif kind in ('warning', 'backup'):
                    self.log_message(('原内容备份：' if kind == 'backup' else '提示：') + str(value))
                elif kind == 'report':
                    self.last_report = value
                    self.log_message('校验记录：' + value)
                elif kind == 'done':
                    self.progress['value'] = 100
                    self.status.set(f"完成：{value['characters']} 个字符，{value['lines']} 行，全文回读一致。未提交答案。")
                    self.log_message(self.status.get())
                    if value['warning']:
                        self.log_message(value['warning'])
                elif kind in ('error', 'cancelled'):
                    self.status.set(('已停止：' if kind == 'error' else '') + value)
                    self.log_message(self.status.get())
                elif kind == 'finished':
                    self.set_busy(False)
                    self.root.deiconify()
                    self.root.lift()
                    self.refresh_environment()
        except queue.Empty:
            pass
        if self.closing:
            if self.worker is None or not self.worker.is_alive():
                self.root.destroy()
                return
        self.root.after(70, self.poll)

    def log_message(self, message):
        self.log.configure(state='normal')
        self.log.insert('end', f'[{datetime.now():%H:%M:%S}] {message}\n\n')
        self.log.see('end')
        self.log.configure(state='disabled')

    def notify_error(self, error):
        self.status.set(str(error))
        self.log_message(str(error))

    def open_records(self):
        try:
            self.records.mkdir(parents=True, exist_ok=True)
            subprocess.Popen(['/usr/bin/open', str(self.records)])
        except Exception as error:
            self.notify_error(error)

    def on_close(self):
        self.closing = True
        self.stop()
        if self.worker is None or not self.worker.is_alive():
            self.root.destroy()


def main():
    if sys.platform != 'darwin':
        raise SystemExit('This release is for macOS only.')
    root = tk.Tk()
    App(root)
    root.mainloop()
