"""One-shot, identity-bound rule-gate to world re-export queue. No trainer launch."""
import json,os,subprocess,sys,time
from pathlib import Path

root=Path('/root/autodl-tmp')
repo=root/'solo_conquest_adventure_v4'
result=Path('/root/solo_group_prior_adventure_v4_gate_20261022/result.json')
status=root/'solo_adventure_v4_world_queue_status.json'
pid=805102
proc=Path('/proc')/str(pid)
def write(**fields):
    temp=status.with_suffix('.pending')
    temp.write_text(json.dumps(dict(pid=os.getpid(),time=time.time(),**fields)))
    temp.replace(status)
def identity():
    try:return (proc/'cmdline').read_bytes(),(proc/'stat').read_text().split(') ',1)[1].split()[19]
    except FileNotFoundError:return None
initial=identity()
if initial is not None:
    assert b'run_solo_group_collection_gate_v4.py' in initial[0]
    assert b'solo_conquest_adventure_v4' in initial[0]
while initial is not None and identity()==initial:
    write(state='waiting_live_seven_hero_gate',gate_pid=pid)
    time.sleep(20)
if not result.exists() or not json.loads(result.read_text()).get('passed'):
    write(state='gate_failed_or_incomplete_no_migration');sys.exit(1)
out=root/'solo_world1200_v4_preserved_splits'
if out.exists():
    write(state='output_already_exists_no_duplicate');sys.exit(1)
env=dict(os.environ,PATH=str(root/'dotnet')+':'+os.environ['PATH'],PYTHONPATH=str(repo/'tools/BattleRl'))
with (root/'solo_adventure_v4_world_migration.log').open('x') as log:
    child=subprocess.Popen([sys.executable,str(root/'migrate_solo_world_manifests_v4.py'),
        '--source',str(root/'solo_world1200_v2_preserved_splits'),'--out',str(out),
        '--repo',str(repo),'--dll',str(repo/'native/SoloConquestRl.dll')],
        env=env,stdout=log,stderr=subprocess.STDOUT)
    write(state='verifying_1200_worlds_8400_resets',child_pid=child.pid)
    code=child.wait()
write(state='migration_complete' if code==0 and (out/'index.json').exists() else 'migration_failed',returncode=code)
