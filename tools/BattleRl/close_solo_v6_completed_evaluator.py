"""Close the specific idle evaluator after its failed trainer and completed eval."""
import json, os, signal
from pathlib import Path
root=Path('/root/mageknight_solo_conquest_A_enemyshuffle_v6_100_20261022')
assert not Path('/proc/931976').exists(), 'Trainer still present'
summary=json.loads((root/'eval_0000/summary.json').read_text())
assert summary['completed']==700 and summary['errors']==0 and summary['parameters_unchanged']
for request in (root/'evaluation_requests').glob('*.json'):
    assert (root/('eval_'+request.stem)/'summary.json').exists(), 'Pending evaluation'
proc=Path('/proc/932107')
stat=(proc/'stat').read_text().split(') ',1)[1].split()
command=(proc/'cmdline').read_bytes().replace(b'\0',b' ').decode()
assert stat[19]=='422885064' and 'evaluate_solo_conquest.py' in command and str(root) in command
(root/'evaluator_retirement.json').write_text(json.dumps(dict(pid=932107,startticks=stat[19],
    command=command,reason='Trainer exited on disk protection; all requested evaluations complete',
    completed=700,errors=0)))
os.kill(932107,signal.SIGTERM)
print('Requested stop of verified idle evaluator 932107 only')
