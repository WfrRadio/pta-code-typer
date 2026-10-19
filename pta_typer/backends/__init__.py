"""Native input backends. No browser JavaScript injection or automatic submit."""
import sys
from ..core import Stopped

BROWSERS = {
    'auto': '自动识别', 'edge': 'Microsoft Edge', 'chrome': 'Google Chrome',
    'brave': 'Brave', 'chromium': 'Chromium', 'safari': 'Safari',
}


def create_backend(control):
    if sys.platform == 'darwin':
        from .macos import MacKeyboard
        return MacKeyboard(control)
    raise Stopped('当前版本仅适配 macOS。')
