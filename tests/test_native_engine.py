import io
import json
from pathlib import Path
import tempfile
import threading
import unittest
from unittest.mock import Mock, patch

from native_engine import EngineServer, main


class NativeEngineTests(unittest.TestCase):
    def test_invalid_code_is_rejected_before_worker(self):
        server = EngineServer(io.StringIO(), keyboard_factory=Mock())
        for code in ('', '  ', 'x\0', 'x' * (256 * 1024 + 1)):
            with self.assertRaises(ValueError):
                server.handle({'command': 'start', 'code': code})
        server.keyboard_factory.assert_not_called()

    def test_success_streams_events_and_closes_keyboard(self):
        output = io.StringIO()
        keyboard = Mock()
        runner = Mock(return_value={'characters': 1, 'lines': 1})
        server = EngineServer(output, keyboard_factory=Mock(return_value=keyboard), runner=runner)
        with tempfile.TemporaryDirectory() as folder, patch('native_engine.reports_directory', return_value=Path(folder)):
            server.handle({'command': 'start', 'code': 'x'})
            server.worker.join(2)
        messages = [json.loads(line) for line in output.getvalue().splitlines()]
        self.assertEqual([m['event'] for m in messages], ['done', 'finished'])
        keyboard.close.assert_called_once()

    def test_cancel_reaches_active_worker(self):
        started = threading.Event()
        output = io.StringIO()

        def runner(keyboard, code, options, directory, emit):
            started.set()
            server.control.event.wait(2)
            server.control.check()

        server = EngineServer(output, keyboard_factory=Mock(return_value=Mock()), runner=runner)
        with tempfile.TemporaryDirectory() as folder, patch('native_engine.reports_directory', return_value=Path(folder)):
            server.handle({'command': 'start', 'code': 'x'})
            self.assertTrue(started.wait(2))
            server.handle({'command': 'cancel'})
            server.worker.join(2)
        events = [json.loads(line)['event'] for line in output.getvalue().splitlines()]
        self.assertEqual(events, ['cancelled', 'finished'])

    def test_failure_still_finishes_and_releases_keyboard(self):
        output = io.StringIO()
        keyboard = Mock()
        server = EngineServer(output, keyboard_factory=Mock(return_value=keyboard), runner=Mock(side_effect=RuntimeError('focus')))
        with tempfile.TemporaryDirectory() as folder, patch('native_engine.reports_directory', return_value=Path(folder)):
            server.handle({'command': 'start', 'code': 'x'})
            server.worker.join(2)
        self.assertEqual([json.loads(line)['event'] for line in output.getvalue().splitlines()], ['error', 'finished'])
        keyboard.close.assert_called_once()

    def test_rejected_start_still_tells_ui_to_finish(self):
        output = io.StringIO()
        with patch('native_engine.sys.stdin', io.StringIO('{"command":"start","code":""}\n')), patch('native_engine.sys.stdout', output):
            main()
        self.assertEqual([json.loads(line)['event'] for line in output.getvalue().splitlines()], ['error', 'finished'])


if __name__ == '__main__':
    unittest.main()
