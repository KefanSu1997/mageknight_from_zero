"""Explicit user-authorized retired runs only; preserve current/runtime inputs."""
import json,shutil,time,zipfile
from pathlib import Path
import psutil
root=Path('/root/autodl-tmp/mageknight').resolve()
names=['remote_repaired_trainonly300','remote_context_pilot200','remote_partitioned_clip61','remote_public_state71','remote_green_enhanced300',
       'optimization_stage_20261014/parent_snapshot','optimization_stage_20261014/deployment_snapshot','optimization_stage_20261014/context_parent_snapshot',
       'optimization_stage_20261014/trainonly_value_pair','optimization_stage_20261014/temperature_probe']
targets=[(root/n).resolve() for n in names]
for p in targets:
    assert p.is_relative_to(root) and p!=root and not (root/str(p.relative_to(root))).is_symlink()
for proc in psutil.process_iter(['pid','cmdline']):
    if proc.pid==psutil.Process().pid:continue
    command=' '.join(proc.info['cmdline'] or [])
    assert not any(str(p) in command or p.name in command for p in targets), (proc.pid,command)
# Check concrete constructor dependencies in the actual saved model data.
dependencies=[]
def scan(value):
    if isinstance(value,dict):
        for v in value.values():scan(v)
    elif isinstance(value,list):
        for v in value:scan(v)
    elif isinstance(value,str) and len(value)<4096 and ('/' in value or '\\' in value):
        path=Path(value)
        if path.exists():dependencies.append(path.resolve())
for checkpoint in [root/'remote_final_semantic_fast300/checkpoint.zip',root/'remote_final_semantic_fast300/behavior_0050_step80646.zip',root/'remote_final_semantic300/checkpoint.zip',root/'remote_final_semantic300/initial_checkpoint.zip']:
    with zipfile.ZipFile(checkpoint) as z:scan(json.loads(z.read('data')))
for dep in dependencies:assert not any(dep==p or dep.is_relative_to(p) for p in targets),str(dep)
report=dict(time=time.time(),authorization='User explicitly authorized listed retired experiments and optimization snapshots on 2026-10-15',dependencies=[str(p) for p in sorted(set(dependencies))],targets=[],free_before=shutil.disk_usage(root).free)
for p in targets:
    if not p.exists():continue
    size=sum(f.stat().st_size for f in p.rglob('*') if f.is_file())
    report['targets'].append(dict(path=str(p),bytes=size))
(root/'authorized_cleanup_plan_20261015.json').write_text(json.dumps(report,indent=2))
for item in report['targets']:
    shutil.rmtree(item['path'])
    assert not Path(item['path']).exists()
report.update(free_after=shutil.disk_usage(root).free,complete=True)
(root/'authorized_cleanup_complete_20261015.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
