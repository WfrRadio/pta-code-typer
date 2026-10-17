#!/usr/bin/env python3
"""Desktop entry point; diagnostics can run without opening a window."""
import json
import platform
import sys

if __name__ == '__main__':
    if '--diagnostics' in sys.argv:
        import tkinter
        from pta_typer.backends.macos import permission_status
        print(json.dumps({'platform': platform.platform(), 'python': platform.python_version(),
                          'frozen': bool(getattr(sys, 'frozen', False)),
                          'tk': tkinter.Tcl().eval('info patchlevel'),
                          'accessibility': permission_status()}, ensure_ascii=False))
    else:
        from pta_typer.gui import main
        main()
