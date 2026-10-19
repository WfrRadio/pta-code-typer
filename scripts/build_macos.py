#!/usr/bin/env python3
"""Build the native SwiftUI host and its bundled private Python worker."""
from pathlib import Path
import plistlib
import platform
import re
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parent.parent
VERSION = re.search(r"__version__ = '([0-9.]+)'", (ROOT/'pta_typer/__init__.py').read_text()).group(1)
if sys.platform != 'darwin':
    raise SystemExit('Build on macOS with Command Line Tools and a macOS 26+ SDK.')
import PyInstaller.__main__

build = ROOT/'build'
build.mkdir(exist_ok=True)
icon = ROOT/'assets/app.icns'
if not icon.exists():
    subprocess.run([sys.executable, str(ROOT/'scripts/make_icon.py')], check=True)
architecture = platform.machine()
swift_binary = build/'PTACodeTyper'
subprocess.run([
    '/usr/bin/xcrun', 'swiftc', '-swift-version', '5', '-parse-as-library', '-O',
    '-target', f'{architecture}-apple-macosx26.0',
    '-module-cache-path', str(build/'swift-module-cache'),
    str(ROOT/'native/PTACodeTyper.swift'), '-o', str(swift_binary),
], check=True)
PyInstaller.__main__.run([
    str(ROOT/'native_engine.py'), '--name', 'PTAInputEngine', '--console', '--onedir', '--noconfirm',
    '--paths', str(ROOT), '--distpath', str(build/'engine-dist'),
    '--workpath', str(build/'engine-work'), '--specpath', str(build),
])
app = ROOT/'dist/PTA Code Typer.app'
if app.exists():
    shutil.rmtree(app)
contents = app/'Contents'
(contents/'MacOS').mkdir(parents=True)
resources = contents/'Resources'
resources.mkdir()
shutil.copy2(swift_binary, contents/'MacOS/PTACodeTyper')
shutil.copytree(build/'engine-dist/PTAInputEngine', resources/'Engine', symlinks=True)
shutil.copy2(icon, resources/'app.icns')
info = {
    'CFBundleName': 'PTA Code Typer', 'CFBundleDisplayName': 'PTA Code Typer',
    'CFBundleIdentifier': 'org.ptacodetyper.desktop', 'CFBundleExecutable': 'PTACodeTyper',
    'CFBundlePackageType': 'APPL', 'CFBundleShortVersionString': VERSION, 'CFBundleVersion': VERSION,
    'CFBundleIconFile': 'app.icns', 'NSHighResolutionCapable': True,
    'LSMinimumSystemVersion': '26.0', 'LSApplicationCategoryType': 'public.app-category.developer-tools',
    'NSAppleEventsUsageDescription': '读取你选定浏览器的当前 PTA 标签页地址，避免向其他网页输入代码。不会提交答案。',
}
(contents/'Info.plist').write_bytes(plistlib.dumps(info))
subprocess.run(['/usr/bin/codesign', '--force', '--sign', '-', '--entitlements',
                str(ROOT/'scripts/entitlements.plist'), str(app)], check=True)
subprocess.run(['/usr/bin/codesign', '--verify', '--deep', '--strict', str(app)], check=True)
subprocess.run(['/usr/bin/ditto', '-c', '-k', '--norsrc', '--noextattr', '--noqtn', '--keepParent',
                str(app), str(ROOT/f'dist/PTA Code Typer-macOS-{architecture}.zip')], check=True)
print(f'Native SwiftUI application ready: {app}')
