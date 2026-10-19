#!/usr/bin/env python3
"""Desktop entry point; diagnostics can run without opening a window."""
import json
import platform
import sys
from pathlib import Path
import subprocess

if __name__ == '__main__':
    if '--diagnostics' in sys.argv:
        from pta_typer.backends.macos import permission_status
        print(json.dumps({'platform': platform.platform(), 'python': platform.python_version(),
                          'frozen': bool(getattr(sys, 'frozen', False)),
                          'ui': 'SwiftUI / Liquid Glass',
                          'accessibility': permission_status()}, ensure_ascii=False))
    elif '--legacy-gui' in sys.argv:
        from pta_typer.gui import main
        main()
    else:
        app = Path(__file__).resolve().parent/'dist/PTA Code Typer.app'
        if not app.exists():
            raise SystemExit('请先按 readme.md 构建原生 App：python scripts/build_macos.py')
        subprocess.run(['/usr/bin/open', str(app)], check=True)
