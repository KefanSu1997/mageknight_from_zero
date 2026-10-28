"""Retire only the abandoned LLM gate after a fresh complete H receipt."""
import hashlib,json,shutil,sys
from pathlib import Path
ROOT=Path('/root/mageknight_llm_lora_real_gate_boundedreplay_fresh_ev128_100_20261021')
STATE=Path('/root/autodl-tmp/closed_bounded_retirement')
STATE.mkdir(exist_ok=True)
def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(8*1024*1024),b''):h.update(block)
    return h.hexdigest()
def snapshot():
    if ROOT.resolve()!=ROOT or not ROOT.is_dir():raise ValueError('Unexpected retirement target')
    for proc in Path('/proc').glob('[0-9]*/cmdline'):
        try:command=proc.read_bytes()
        except (FileNotFoundError,PermissionError):continue
        if str(ROOT).encode() in command:raise RuntimeError('A process still references target: '+str(proc.parent))
    files=[]
    for path in sorted(ROOT.rglob('*')):
        if path.is_symlink():raise ValueError('Refuse symlink')
        if path.is_file():files.append(dict(path=path.relative_to(ROOT).as_posix(),bytes=path.stat().st_size,sha256=sha(path)))
    return dict(root=str(ROOT),files=files)
if sys.argv[1]=='manifest':
    value=snapshot();(STATE/'manifest.json').write_text(json.dumps(value));print(json.dumps(dict(files=len(value['files']),bytes=sum(r['bytes'] for r in value['files']))))
elif sys.argv[1]=='commit':
    expected=json.loads((STATE/'manifest.json').read_text());receipt=json.loads((STATE/'receipt.json').read_text())
    if receipt.get('verified') is not True or receipt.get('manifest')!=expected:raise ValueError('H verification missing')
    if snapshot()!=expected:raise ValueError('Closed source changed after verification')
    shutil.rmtree(ROOT)
    result=dict(removed=str(ROOT),files=len(expected['files']),bytes=sum(r['bytes'] for r in expected['files']),local=receipt['local'])
    (STATE/'complete.json').write_text(json.dumps(result));print(json.dumps(result))
else:raise ValueError('Unknown mode')
