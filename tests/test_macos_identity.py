"""Regressions for launch-time foreground caches and code-sign clone paths."""
import ctypes as C
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

from pta_typer.core import Stopped
from pta_typer.backends.macos import MacKeyboard, browser_url_script


class BrowserIdentityTests(unittest.TestCase):
    def test_foreground_is_queried_on_every_call(self):
        keyboard = object.__new__(MacKeyboard)
        pids = iter([17, 42, 17])

        def get_pid(serial, output):
            C.cast(output, C.POINTER(C.c_int)).contents.value = next(pids)
            return 0

        keyboard.ax = SimpleNamespace(GetFrontProcess=Mock(return_value=0), GetProcessPID=Mock(side_effect=get_pid))
        self.assertEqual([keyboard.front_pid() for _ in range(3)], [17, 42, 17])
        self.assertEqual(keyboard.ax.GetFrontProcess.call_count, 3)

    def test_native_foreground_failure_stops(self):
        keyboard = object.__new__(MacKeyboard)
        keyboard.ax = SimpleNamespace(GetFrontProcess=Mock(return_value=-1), GetProcessPID=Mock())
        with self.assertRaises(Stopped):
            keyboard.front_pid()
        keyboard.ax.GetProcessPID.assert_not_called()

    def test_zero_pid_stops(self):
        keyboard = object.__new__(MacKeyboard)
        keyboard.ax = SimpleNamespace(GetFrontProcess=Mock(return_value=0), GetProcessPID=Mock(return_value=0))
        with self.assertRaises(Stopped):
            keyboard.front_pid()

    def keyboard_for_lock(self, bundle='com.microsoft.edgemac'):
        keyboard = object.__new__(MacKeyboard)
        keyboard.control = Mock()
        keyboard.front_pid = Mock(return_value=42)
        keyboard.bundle_for_pid = Mock(return_value=bundle)
        keyboard.attribute = Mock(return_value=123)
        keyboard.cf_text = Mock(return_value='AXTextArea')
        keyboard.cf = Mock()
        keyboard.ax = Mock()
        keyboard.attributes = {'AXValue': 10}
        keyboard.check = Mock()
        keyboard.test_url = None

        def editable(element, attribute, output):
            C.cast(output, C.POINTER(C.c_bool)).contents.value = True
            return 0

        keyboard.ax.AXUIElementIsAttributeSettable.side_effect = editable
        return keyboard

    def test_bundle_identity_does_not_depend_on_executable_path(self):
        keyboard = self.keyboard_for_lock()
        with patch('pta_typer.backends.macos.subprocess.run', return_value=SimpleNamespace(stdout='https://pintia.cn/')):
            keyboard.lock_target('edge')
        self.assertEqual(keyboard.target_pid, 42)
        self.assertEqual(keyboard.browser_name, 'Microsoft Edge')
        # No libproc/path object exists in this fixture: renamed/clone paths
        # cannot reject an otherwise valid browser identity.

    def test_bundle_prefix_is_not_accepted(self):
        keyboard = self.keyboard_for_lock('com.microsoft.edgemac.other')
        with patch('pta_typer.backends.macos.subprocess.run') as run, self.assertRaises(Stopped):
            keyboard.lock_target('edge')
        run.assert_not_called()

    def test_selected_browser_still_must_match(self):
        keyboard = self.keyboard_for_lock()
        with patch('pta_typer.backends.macos.subprocess.run') as run, self.assertRaises(Stopped):
            keyboard.lock_target('chrome')
        run.assert_not_called()

    def test_safari_uses_current_tab_in_both_auto_and_selected_modes(self):
        for selected in ('auto', 'safari'):
            keyboard = self.keyboard_for_lock('com.apple.Safari')
            with patch('pta_typer.backends.macos.subprocess.run', return_value=SimpleNamespace(stdout='https://pintia.cn/')) as run:
                keyboard.lock_target(selected)
            self.assertEqual(keyboard.browser_name, 'Safari')
            script = run.call_args.args[0][-1]
            self.assertIn('"com.apple.Safari"', script)
            self.assertIn('current tab of front window', script)
            self.assertNotIn('active tab', script)

    def test_safari_non_pta_page_stops_before_editor_access(self):
        keyboard = self.keyboard_for_lock('com.apple.Safari')
        with patch('pta_typer.backends.macos.subprocess.run', return_value=SimpleNamespace(stdout='https://example.com/')), self.assertRaises(Stopped):
            keyboard.lock_target('safari')
        keyboard.ax.AXUIElementCreateApplication.assert_not_called()

    def test_safari_automation_denied_stops_before_editor_access(self):
        import subprocess
        keyboard = self.keyboard_for_lock('com.apple.Safari')
        with patch('pta_typer.backends.macos.subprocess.run', side_effect=subprocess.CalledProcessError(1, 'osascript')), self.assertRaises(Stopped):
            keyboard.lock_target('safari')
        keyboard.ax.AXUIElementCreateApplication.assert_not_called()

    def test_unknown_browser_has_no_script(self):
        with self.assertRaises(Stopped):
            browser_url_script('unknown')

    def test_switching_application_still_stops_before_typing(self):
        keyboard = object.__new__(MacKeyboard)
        keyboard.control = Mock()
        keyboard.cg = Mock()
        keyboard.cg.CGEventSourceKeyState.return_value = False
        keyboard.target_pid = 42
        keyboard.front_pid = Mock(return_value=17)
        with self.assertRaises(Stopped):
            keyboard.check()
        keyboard.cg.CGEventCreate.assert_not_called()


if __name__ == '__main__':
    unittest.main()
