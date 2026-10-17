"""Explicit native-backend regression; restores the editor, never submits."""
from pathlib import Path
import json
import subprocess
import sys
sys.path.insert(0,str(Path(__file__).resolve().parent.parent))
from pta_typer.core import Control, Options, normalize_code, run_typing
from pta_typer.backends.macos import MacKeyboard

root=Path(__file__).resolve().parent.parent
subprocess.run(['osascript','-e','tell application "Microsoft Edge" to activate'],check=True)
k=MacKeyboard(Control())
results=[]
original=None
try:
    k.lock_target('edge')
    original=k.read_editor()
    (root/'data/qa/editor-original.txt').write_text(original,encoding='utf-8')
    for filename in ['basic.c','escapes.c']:
        source=(root/'tests/fixtures'/filename).read_text(encoding='utf-8')
        print('LIVE',filename,flush=True)
        result=run_typing(k,source,Options(browser='edge',retries=0),root/'data/qa/source-reports',
                          emit=lambda kind,value:print(kind,value,flush=True) if kind in ('warning','progress','report') else None,
                          countdown=False)
        assert k.read_editor()==normalize_code(source)
        results.append({'fixture':filename,'exact_match':True,'report':result['report']})
finally:
    try:
        if original is not None:
            print('RESTORING EDITOR',flush=True)
            k.target_pid=None
            if k.editor:
                k.cf.CFRelease(k.editor); k.editor=None
            if k.application:
                k.cf.CFRelease(k.application); k.application=None
            subprocess.run(['osascript','-e','tell application "Microsoft Edge" to activate'],check=True)
            result=run_typing(k,original,Options(browser='edge',retries=0),root/'data/qa/source-reports',countdown=False)
            assert k.read_editor()==original
            results.append({'restored_original':True,'characters':len(original),'report':result['report']})
            print('RESTORED',len(original),flush=True)
    finally:
        k.close()
        (root/'data/qa/source-validation.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
