"""One-use identity-checked stop of the defective solo v5 formal run."""
import json,time,os,signal
from pathlib import Path
import psutil
root=Path('/root/mageknight_solo_conquest_A_score_v5_100_20261022')
targets=[]
for pid,script,ticks in ((854042,'train_solo_conquest.py','422458367'),(854186,'evaluate_solo_conquest.py','422458967')):
    p=psutil.Process(pid);cmd=p.cmdline()
    assert script in ' '.join(cmd) and str(root) in cmd
    assert Path(f'/proc/{pid}/stat').read_text().split(') ',1)[1].split()[19]==ticks
    children=p.children()
    for c in children:
        assert any('SoloConquestRl.dll' in x for x in c.cmdline()),c.cmdline()
    targets.append((p,children))
record=dict(time=time.time(),reason='SoloEpisodeFactory passes unshuffled initial enemy inventory to EnemyTokenSupply; initial Draw ignores seed until recycling. Formal world randomization invalid.',
    processes=[dict(pid=p.pid,created=p.create_time(),cmd=p.cmdline(),children=[c.pid for c in cs]) for p,cs in targets],
    evidence={name:json.loads((root/name).read_text()) for name in ('phase.json','optimizer_progress.json') if (root/name).exists()},
    preservation='Initial checkpoint and full128 rollout retained. In-progress unsaved PPO is not a completed update.')
(root/'enemy_shuffle_defect_retirement.json').write_text(json.dumps(record,indent=2))
for p,children in targets:
    p.send_signal(signal.SIGTERM)
    for child in children:
        if child.is_running():child.send_signal(signal.SIGTERM)
gone,alive=psutil.wait_procs([p for p,cs in targets]+[c for p,cs in targets for c in cs],timeout=15)
record['remaining_pids']=[p.pid for p in alive]
(root/'enemy_shuffle_defect_retirement.json').write_text(json.dumps(record,indent=2))
print(json.dumps(record))
