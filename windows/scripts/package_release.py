#!/usr/bin/env python3
"""Build a source-only repair bundle. Never claims Windows binaries were built."""
from pathlib import Path
import hashlib
import json
import zipfile

WINDOWS = Path(__file__).resolve().parent.parent
ROOT = WINDOWS.parent
VERSION = "0.1.2"
TAG = "windows-v0.1.2-preview.1"
EXTENSIONS = {".cs", ".xaml", ".csproj", ".sln", ".props", ".json", ".manifest", ".wxs", ".ps1", ".cmd", ".py", ".md", ".c", ".rtf"}
EXCLUDED = {"bin", "obj", ".vs", ".tools", ".wix", "publish", "artifacts", "generated", "output", "TestResults", "__pycache__", ".git", "release", "build-logs"}

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    output = WINDOWS / "release" / TAG
    output.mkdir(parents=True, exist_ok=True)
    files = [p for p in WINDOWS.rglob("*") if p.is_file()
             and not any(part in EXCLUDED for part in p.relative_to(WINDOWS).parts)
             and (p.suffix.lower() in EXTENSIONS or p.name == ".gitignore")]
    prefix = f"PTA-Code-Typer-{VERSION}"
    members = {f"{prefix}/windows/{p.relative_to(WINDOWS).as_posix()}": p for p in files}
    members[f"{prefix}/LICENSE"] = ROOT / "LICENSE"
    handoff = ROOT / "docs/WINDOWS_DEVELOPMENT_HANDOFF.zh-CN.md"
    if handoff.is_file(): members[f"{prefix}/docs/{handoff.name}"] = handoff
    archive = output / f"PTA-Code-Typer-{VERSION}-windows-source.zip"
    with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for name, path in sorted(members.items()): z.write(path, name)
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None
        for name, path in members.items(): assert z.read(name) == path.read_bytes()
    manifest = {"version": VERSION, "tag": TAG, "artifact_kind": "source-only",
                "windows_exe_generated": False, "windows_msi_generated": False,
                "archive": archive.name, "sha256": sha(archive),
                "files": {name: sha(path) for name, path in sorted(members.items())}}
    (output / "PACKAGE_MANIFEST.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (output / "SHA256SUMS.txt").write_text(f"{sha(archive)}  {archive.name}\n", encoding="ascii")
    print(archive)
    print(f"Verified {len(members)} source files; no EXE or MSI is claimed.")

if __name__ == "__main__": main()
