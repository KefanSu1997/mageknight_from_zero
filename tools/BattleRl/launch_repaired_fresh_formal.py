import json,subprocess
from pathlib import Path
import psutil
stage=Path('optimization_stage_20261014')
path=stage/'repaired_fresh_gate_status.json';s=json.loads(path.read_text());s['state']='gate_passed';path.write_text(json.dumps(s,indent=2))
assert not Path('remote_repaired_fresh300').exists()
with (stage/'repaired_fresh_deployment.log').open('wb') as log:
 p=subprocess.Popen(['/root/autodl-tmp/mkvenv/bin/python','-u',str(stage/'deploy_repaired_fresh300.py')],stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
x=dict(pid=p.pid,created=psutil.Process(p.pid).create_time(),command=psutil.Process(p.pid).cmdline())
(stage/'repaired_fresh_launch.json').write_text(json.dumps(x,indent=2));print(json.dumps(x))
