import json,subprocess
from pathlib import Path
import psutil
stage=Path('optimization_stage_20261014')
assert not Path('remote_repaired_fresh_gate32').exists()
for p in psutil.process_iter(['pid','cmdline']):
 c=p.info['cmdline'] or []
 if c and ('deploy_root_entropy240.py' in ' '.join(c) or 'deploy_conditioned_after_gate_v2.py' in ' '.join(c)) and 'python' in c[0]:raise RuntimeError('Previous trainer still active')
with (stage/'repaired_fresh_gate.log').open('wb') as log:
 p=subprocess.Popen(['/root/autodl-tmp/mkvenv/bin/python','-u',str(stage/'run_repaired_fresh_entry.py'),'--out','remote_repaired_fresh_gate32','--updates','1','--workers','32','--gate'],stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
x=dict(state='gate_running',pid=p.pid,created=psutil.Process(p.pid).create_time(),command=psutil.Process(p.pid).cmdline())
(stage/'repaired_fresh_gate_status.json').write_text(json.dumps(x,indent=2));print(json.dumps(x))
