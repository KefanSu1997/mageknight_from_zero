import json,time,os,signal
from pathlib import Path
import psutil
out=Path('/root/mageknight_solo_conquest_A_forest100_20261022')
targets=[(742764,'train_solo_conquest.py','solo_conquest_A_forest_v3'),(742881,'evaluate_solo_conquest.py','solo_conquest_A_forest_v3'),(813757,'run_solo_group_ppo_v4_gate.py','solo_group_ppo_v4_stage')]
record=dict(time=time.time(),reason='Confirmed scoring defect: dummy-announced end incorrectly receives no-announcement +5; 77 of initial128 affected. Preserve incomplete PPO, restart fresh after correction.',processes=[])
stop=[]
for pid,script,root in targets:
    try:p=psutil.Process(pid)
    except psutil.NoSuchProcess:continue
    cmd=' '.join(p.cmdline());assert script in cmd and root in cmd,(pid,cmd)
    children=p.children(recursive=True)
    for c in children:
        command=' '.join(c.cmdline())
        assert 'SoloConquestRl.dll' in command and 'solo_conquest_A_forest_v3' in command,(c.pid,command)
    record['processes'].append(dict(pid=pid,created=p.create_time(),command=cmd,children=[dict(pid=c.pid,created=c.create_time(),command=' '.join(c.cmdline())) for c in children]))
    stop.extend(children);stop.append(p)
(out/'reward_defect_retirement.json').write_text(json.dumps(record,indent=2))
for p in stop:
    try:p.terminate()
    except psutil.NoSuchProcess:pass
_,alive=psutil.wait_procs(stop,timeout=10)
record['remaining']=[p.pid for p in alive]
(out/'reward_defect_retirement.json').write_text(json.dumps(record,indent=2));print(json.dumps(record))
