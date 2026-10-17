# Third-party components

PTA Code Typer source code and its generated bracket icon are distributed under
the MIT License in `LICENSE`. Third-party components retain their own licenses.

The macOS v0.1.0 arm64 executable includes:

| Component | Version | License text |
| --- | --- | --- |
| CPython and bundled standard library | 3.12.14 | `licenses/Python-3.12-LICENSE.txt` |
| Tcl | 9.0.4 | `licenses/Tcl-9.0.4-license.terms` |
| Tk | 9.0.4 | `licenses/Tk-9.0.4-license.terms` |
| PyInstaller bootloader and embedded loader/runtime hooks | 6.22.3 | `licenses/PyInstaller-COPYING.txt` |

PyInstaller's copying terms include its bootloader exception and the licensing
terms for embedded runtime hooks. The full upstream file is included unchanged.
PyInstaller itself is a build dependency; end users do not need to install it.
The build tool's other dependencies are not shipped as a Python development
environment in either archive.

Upstream references:

- Python: https://www.python.org/ and https://docs.python.org/3/license.html
- Tcl 9.0.4: https://github.com/tcltk/tcl/tree/core-9-0-4
- Tk 9.0.4: https://github.com/tcltk/tk/tree/core-9-0-4
- PyInstaller: https://github.com/pyinstaller/pyinstaller/tree/v6.22.3

Python and PyInstaller license files were copied from the installed versions
used for this build. Tcl/Tk license files were obtained from the version tags
above. Original notices in the bundled files remain intact. macOS frameworks
and system libraries are loaded from the operating system and are not bundled.

For future builds with different runtime versions, review and update this list
and its accompanying license texts before distributing the result.
