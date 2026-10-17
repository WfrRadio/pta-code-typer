#!/usr/bin/env python3
"""Package reviewed source and an existing, signed macOS app; never rebuild it."""
from pathlib import Path
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import zipfile

ROOT = Path(__file__).resolve().parent.parent
ROOT_FILES = (
    '.gitignore', 'LICENSE', 'THIRD_PARTY_NOTICES.md', 'readme.md',
    'main.py', 'requirements-build.txt', '打开应用.command', '源码启动.command',
)
# Only these directories and file types enter the public source archive.
SOURCE_DIRS = {
    'pta_typer': {'.py'}, 'tests': {'.py', '.c', '.json'},
    'scripts': {'.py', '.plist'}, 'assets': {'.png', '.icns'},
    'docs': {'.md'}, 'licenses': {'.txt', '.terms'},
}
FORBIDDEN_PARTS = {'.git', '.venv', '__pycache__', 'data', 'build', 'dist', 'release'}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def source_files():
    result = [ROOT / name for name in ROOT_FILES]
    for dirname, extensions in SOURCE_DIRS.items():
        for path in sorted((ROOT / dirname).rglob('*')):
            parts = path.relative_to(ROOT).parts
            if path.is_file() and path.suffix in extensions and not set(parts) & FORBIDDEN_PARTS:
                if path.is_symlink():
                    raise RuntimeError(f'Source symlink requires review: {path.name}')
                result.append(path)
    for path in result:
        if not path.is_file():
            raise RuntimeError(f'Missing source file: {path.name}')
    return sorted(result)


def app_manifest(app):
    result = {}
    for path in sorted(app.rglob('*')):
        relative = path.relative_to(app).as_posix()
        if path.is_symlink():
            if not path.resolve().is_relative_to(app.resolve()):
                raise RuntimeError(f'External app symlink: {relative}')
            result[relative] = {'symlink': os.readlink(path)}
        elif path.is_file():
            if path.name == '.DS_Store' or path.suffix in {'.log'}:
                raise RuntimeError(f'Unexpected app file: {relative}')
            result[relative] = {'sha256': digest(path)}
    return result


def main():
    if sys.platform != 'darwin':
        raise SystemExit('Run on macOS after building and testing the .app.')
    version_text = (ROOT / 'pta_typer/__init__.py').read_text()
    version = re.search(r"__version__ = '([^']+)'", version_text).group(1)
    if version != '0.1.0':
        raise SystemExit('Update the versioned docs and runtime notices before packaging another version.')
    tag = f'v{version}'
    app = ROOT / 'dist/PTA Code Typer.app'
    binary = app / 'Contents/MacOS/PTA Code Typer'
    architectures = subprocess.check_output(['/usr/bin/lipo', '-archs', str(binary)], text=True).strip()
    if architectures != 'arm64':
        raise SystemExit('This release recipe and its install notes cover arm64 only.')
    subprocess.run(['/usr/bin/codesign', '--verify', '--deep', '--strict', str(app)], check=True)
    output = ROOT / 'release'
    source_name = f'PTA-Code-Typer-{tag}'
    app_name = f'{source_name}-macOS-arm64'
    source_root = output / 'source' / source_name
    app_root = output / 'app' / app_name
    source_zip = output / f'{source_name}-source.zip'
    app_zip = output / f'{app_name}-prerelease.zip'
    # Refuse to overwrite a previous reviewable release by accident.
    if any(p.exists() for p in (source_root, app_root, source_zip, app_zip)):
        raise SystemExit('Release output already exists. Review/move it before packaging again.')
    files = source_files()
    # These markers identify local QA material, not generic upstream license text.
    for path in files:
        if path.suffix in {'.py', '.md', '.json', '.txt', '.command'}:
            value = path.read_text(encoding='utf-8')
            if ('/' + 'Users/') in value or re.search(r'problem-sets/\d{8,}', value):
                raise RuntimeError(f'Local content requires review: {path.relative_to(ROOT)}')
    original_app = app_manifest(app)
    source_root.mkdir(parents=True)
    app_root.mkdir(parents=True)
    for path in files:
        destination = source_root / path.relative_to(ROOT)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, destination)
    with zipfile.ZipFile(source_zip, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for path in files:
            staged = source_root / path.relative_to(ROOT)
            archive.write(staged, f'{source_name}/{path.relative_to(ROOT).as_posix()}')
    copied_app = app_root / app.name
    shutil.copytree(app, copied_app, symlinks=True)
    assert app_manifest(copied_app) == original_app
    for name in ('LICENSE', 'THIRD_PARTY_NOTICES.md'):
        shutil.copy2(ROOT / name, app_root / name)
    shutil.copytree(ROOT / 'licenses', app_root / 'licenses')
    shutil.copy2(ROOT / 'docs/INSTALL_MACOS.zh-CN.md', app_root / '安装说明.md')
    shutil.copy2(ROOT / 'docs/RELEASE_NOTES_v0.1.0.md', app_root / 'RELEASE_NOTES.md')
    build_info = {
        'version': version, 'release_status': 'pre-release',
        'architecture': architectures, 'signing': 'ad-hoc; not notarized',
        'runtime': {'python': '3.12.14', 'tcl_tk': '9.0.4'},
        'executable_sha256': digest(binary),
        'app_content_manifest_sha256': hashlib.sha256(json.dumps(original_app, sort_keys=True).encode()).hexdigest(),
        'repackaged_without_rebuilding': True,
    }
    (app_root / 'BUILD_INFO.json').write_text(json.dumps(build_info, indent=2) + '\n')
    subprocess.run([
        '/usr/bin/ditto', '-c', '-k', '--norsrc', '--noextattr', '--noqtn',
        '--keepParent', str(app_root), str(app_zip),
    ], check=True)
    checksums = ''.join(f'{digest(path)}  {path.name}\n' for path in (source_zip, app_zip))
    (output / 'SHA256SUMS.txt').write_text(checksums)
    manifest = {
        'version': version, 'source_files': {
            p.relative_to(ROOT).as_posix(): digest(p) for p in files
        }, 'app_files': original_app, 'build': build_info,
        'archives': {p.name: digest(p) for p in (source_zip, app_zip)},
    }
    (output / 'PACKAGE_MANIFEST.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print(json.dumps({'source_zip': str(source_zip), 'app_zip': str(app_zip),
                      'source_files': len(files), 'app_manifest_entries': len(original_app)}, indent=2))


if __name__ == '__main__':
    main()
