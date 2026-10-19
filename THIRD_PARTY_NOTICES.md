# Third-party components

PTA Code Typer source code and its generated bracket icon use the MIT License in `LICENSE`. Third-party components retain their own licenses.

The native macOS v0.2.0 arm64 executable includes:

| Component | Version | License text |
| --- | --- | --- |
| CPython and its bundled standard library, in the private worker | 3.12.14 | `licenses/Python-3.12-LICENSE.txt` |
| PyInstaller bootloader and embedded runtime hooks | 6.22.3 | `licenses/PyInstaller-COPYING.txt` |

The full PyInstaller terms include its bootloader exception and runtime-hook licenses. The files are included unchanged from the installed build dependencies.

SwiftUI, AppKit, Swift runtime libraries, SF Symbols, and system frameworks are loaded from macOS and are not separately redistributed in this archive. The native app does not bundle Tcl or Tk. The source retains an optional legacy Tk interface; Tcl/Tk 9.0.4 license texts are preserved in `licenses/` for reference to the older build and that optional interface.

Upstream references:

- Python: https://www.python.org/ and https://docs.python.org/3/license.html
- PyInstaller: https://github.com/pyinstaller/pyinstaller/tree/v6.22.3
- Tcl: https://github.com/tcltk/tcl/tree/core-9-0-4
- Tk: https://github.com/tcltk/tk/tree/core-9-0-4

Review this list and accompanying notices when changing bundled runtime versions.
