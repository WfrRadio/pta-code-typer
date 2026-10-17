from contextlib import redirect_stdout
from pathlib import Path
import json
import tempfile
import threading
import unittest
from unittest.mock import Mock, patch

from pta_typer.core import (Cancelled, ClipboardReader, Control, Mismatch, Options,
    Stopped, is_pta_url, normalize_code, run_typing, task_warning, write_report)


class CoreTests(unittest.TestCase):
    def test_normalize_only_indentation_and_line_endings(self):
        value='\ufeff\t\u00a0x\r\n    printf("A\u00a0B\\n");  \r'
        self.assertEqual(normalize_code(value),'     x\n    printf("A\u00a0B\\n");  \n')

    def test_rejects_control_and_surrogate(self):
        for character in ['\0','\x1b','\x7f','\ud800']:
            with self.subTest(character=repr(character)), self.assertRaises(ValueError):
                normalize_code('x'+character)

    def test_domain_validation(self):
        for url in ['https://pintia.cn/a','https://www.pintia.cn/a','http://pintia.cn/']:
            self.assertTrue(is_pta_url(url))
        for url in ['https://pintia.cn.evil.test','https://evil.test/pintia.cn','file:///pintia.cn','https://pintia.cn@evil.test']:
            self.assertFalse(is_pta_url(url))

    def test_diagnostic_location(self):
        mismatch=Mismatch('a\n(1);','a\n91);')
        self.assertEqual((mismatch.line,mismatch.column),(2,1))

    def test_function_problem_warning(self):
        self.assertTrue(task_warning('https://pintia.cn/problem-sets/1/exam/problems/type/6','int main() {}'))
        self.assertFalse(task_warning('https://pintia.cn/problem-sets/1/exam/problems/type/7','int main() {}'))

    def test_options_reject_invalid_values(self):
        for option in [Options(delay=0),Options(countdown=0),Options(check_every=0),Options(retries=5)]:
            with self.assertRaises(ValueError): option.validate()

    def test_control_cancels_wait(self):
        control=Control(); control.event.set()
        with self.assertRaises(Cancelled): control.wait(10)

    def test_report_saved_exactly(self):
        with tempfile.TemporaryDirectory() as folder:
            p=write_report(folder,'abc','ab)','mismatch')
            self.assertEqual((p/'actual.txt').read_text(),'ab)')
            self.assertEqual(json.loads((p/'report.json').read_text())['column'],3)

    def test_focus_error_before_any_typing(self):
        keyboard=Mock(); keyboard.lock_target.side_effect=Stopped('wrong focus')
        with self.assertRaises(Stopped):
            run_typing(keyboard,'a',Options(),'/not-used',countdown=False)
        keyboard.select_all.assert_not_called()
        keyboard.backspace.assert_not_called()

    def test_backup_precedes_destructive_input(self):
        keyboard=Mock(); keyboard.target_url='https://pintia.cn'; keyboard.browser_name='Edge'
        keyboard.editor_empty.return_value=False; keyboard.read_editor.return_value='old code'
        order=[]
        def backup(*args,**kwargs):
            order.append('backup'); return Path('/local/report')
        def type_it(*args,**kwargs):
            order.append('typing'); return 'new'
        with patch('pta_typer.core.write_report',side_effect=backup),patch('pta_typer.core.type_once',side_effect=type_it):
            run_typing(keyboard,'new',Options(),'/unused',countdown=False)
        self.assertEqual(order[:2],['backup','typing'])

    def test_mismatch_retries_but_focus_failure_does_not(self):
        keyboard=Mock(); keyboard.target_url='https://pintia.cn'; keyboard.browser_name='Edge'; keyboard.editor_empty.return_value=True
        with tempfile.TemporaryDirectory() as folder,patch('pta_typer.core.type_once',side_effect=[Mismatch('a','b'),'a']) as send:
            result=run_typing(keyboard,'a',Options(),folder,countdown=False)
            self.assertEqual(result['characters'],1)
            self.assertGreater(send.call_args_list[1].args[3],send.call_args_list[0].args[3])
        with tempfile.TemporaryDirectory() as folder,patch('pta_typer.core.type_once',side_effect=Stopped('focus')) as send:
            with self.assertRaises(Stopped): run_typing(keyboard,'a',Options(),folder,countdown=False)
            self.assertEqual(send.call_count,1)

    def test_clipboard_reader_requires_stability_and_restores(self):
        reader=ClipboardReader()
        reader.control=Mock(); reader.check=Mock(); reader.select_all=Mock(); reader.copy=Mock(); reader.right=Mock()
        reader.clipboard_get=Mock(side_effect=['original','unexpected','expected','expected'])
        reader.clipboard_set=Mock()
        self.assertEqual(reader.read_editor(),'expected')
        reader.clipboard_set.assert_called_with('original')

    def test_unavailable_copy_never_reports_success(self):
        reader=ClipboardReader(); state={'clipboard':'original'}
        reader.control=Mock(); reader.check=Mock(); reader.select_all=Mock(); reader.copy=Mock(); reader.right=Mock()
        reader.clipboard_get=lambda:state['clipboard']
        reader.clipboard_set=lambda text:state.update(clipboard=text)
        with self.assertRaises(Stopped): reader.read_editor()
        self.assertEqual(state['clipboard'],'original')


if __name__=='__main__': unittest.main()
