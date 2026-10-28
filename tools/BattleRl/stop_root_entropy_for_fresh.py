import json,hashlib
from pathlib import Path
import psutil
root=Path('remote_root_entropy240');stage=Path('optimization_stage_20261014')
p=psutil.Process(137209);assert abs(p.create_time()-1791951671.56)<.02
assert p.cmdline()[-1]=='optimization_stage_20261014/deploy_root_entropy240.py'
children=[]
for c in p.children():
 if any('atomic_adapter_v2_frozen/BattleRl.dll' in a.replace('\\','/') for a in c.cmdline()):children.append((c,c.create_time(),c.cmdline()))
identity=dict(pid=p.pid,created=p.create_time(),command=p.cmdline(),reason='User explicitly requested fresh training; preserve warm branch')
p.terminate();p.wait(timeout=20)
for c,created,args in children:
 try:
  if c.create_time()==created and c.cmdline()==args:c.terminate()
 except psutil.NoSuchProcess:pass
_,alive=psutil.wait_procs([c for c,_,_ in children],timeout=20)
assert not [c for c in alive if c.status()!=psutil.STATUS_ZOMBIE]
last=json.loads((root/'metrics.jsonl').read_text().splitlines()[-1])
identity['last_metrics_update']=last['update']
identity['sha']={n:hashlib.sha256((root/n).read_bytes()).hexdigest() for n in ['checkpoint.zip','continuation.pt','manifest.json']}
(stage/'stopped_root_entropy_for_fresh.json').write_text(json.dumps(identity,indent=2))
(stage/'root_entropy_deployment_status.json').write_text(json.dumps(dict(state='stopped_user_requested_fresh',completed=last['update'],pid=137209)))
print(json.dumps(identity))
