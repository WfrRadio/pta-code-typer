#!/usr/bin/env python3
"""Run with the project's venv Python on macOS."""
from pathlib import Path
import plistlib
import platform
import subprocess
import sys

ROOT=Path(__file__).resolve().parent.parent
if sys.platform!='darwin':
    raise SystemExit('Build this application on macOS.')
import PyInstaller.__main__

icon=ROOT/'assets/app.icns'
if not icon.exists():
    subprocess.run([sys.executable,str(ROOT/'scripts/make_icon.py')],check=True)
PyInstaller.__main__.run([
    str(ROOT/'main.py'),'--name','PTA Code Typer','--windowed','--onedir','--noconfirm',
    '--osx-bundle-identifier','org.ptacodetyper.desktop',
    '--icon',str(icon),'--paths',str(ROOT),
    '--distpath',str(ROOT/'dist'),'--workpath',str(ROOT/'build'),
    '--specpath',str(ROOT/'build'),
])
app=ROOT/'dist/PTA Code Typer.app'
info_path=app/'Contents/Info.plist'
info=plistlib.loads(info_path.read_bytes())
info.update(CFBundleDisplayName='PTA Code Typer',CFBundleShortVersionString='0.1.0',CFBundleVersion='1',
            NSHighResolutionCapable=True,LSApplicationCategoryType='public.app-category.developer-tools',
            NSAppleEventsUsageDescription='读取你选定浏览器的当前 PTA 标签页地址，避免向其他网页输入代码。不会提交答案。')
info_path.write_bytes(plistlib.dumps(info))
subprocess.run(['/usr/bin/codesign','--force','--sign','-', '--entitlements',str(ROOT/'scripts/entitlements.plist'),str(app)],check=True)
subprocess.run(['/usr/bin/codesign','--verify','--deep','--strict',str(app)],check=True)
architecture = platform.machine()
subprocess.run(['/usr/bin/ditto','-c','-k','--sequesterRsrc','--keepParent',str(app),str(ROOT/f'dist/PTA Code Typer-macOS-{architecture}.zip')],check=True)
print(f'\nApplication ready: {app}')
