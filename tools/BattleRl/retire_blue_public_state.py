"""User-authorized retirement of precisely identified blue trainer only."""
import json,time,hashlib
from pathlib import Path
import psutil
root=Path('/root/autodl-tmp/mageknight/remote_public_state71')
p=psutil.Process(167868)
assert p.cmdline()==['/root/autodl-tmp/mkvenv/bin/python','-u','optimization_stage_20261014/resume_public_state71.py']
children=p.children(recursive=True)
assert len(children)==32
for c in children:
    assert c.ppid()==p.pid and c.cmdline()==['dotnet','/root/autodl-tmp/mageknight/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll','--serve-journey-artifacts']
identity=dict(pid=p.pid,created=p.create_time(),cmd=p.cmdline(),children=[dict(pid=c.pid,created=c.create_time(),cmd=c.cmdline()) for c in children])
# Freeze the writer first, preserve file identity, then stop only these handles.
p.suspend()
snapshot={n:hashlib.sha256((root/n).read_bytes()).hexdigest() for n in ['checkpoint.zip','continuation.pt','manifest.json','metrics.jsonl']}
p.terminate()
for c in children:
    if c.is_running():c.terminate()
_,alive=psutil.wait_procs([p]+children,timeout=5)
for q in alive:q.kill()
_,alive=psutil.wait_procs(alive,timeout=5)
assert not alive
assert snapshot=={n:hashlib.sha256((root/n).read_bytes()).hexdigest() for n in snapshot}
report=dict(state='retired_by_user',time=time.time(),identity=identity,files_sha=snapshot,prior_status=json.loads((root/'status.json').read_text()),reason='User abandoned blue method; develop from green structured entity baseline. No automatic blue restart. Preserve all evidence; partial current rollout is not a completed update.')
(root/'retirement.json').write_text(json.dumps(report,indent=2))
(root/'status.json').write_text(json.dumps(dict(state='retired_by_user',completed=report['prior_status'].get('completed'),time=time.time()),indent=2))
print(json.dumps({k:v for k,v in report.items() if k!='identity'}))
