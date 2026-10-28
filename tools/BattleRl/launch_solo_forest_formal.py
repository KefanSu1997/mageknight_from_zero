"""One-shot fresh100 launch after verified world migration; independent eval32."""
import json,os,shutil,subprocess,sys,time
from pathlib import Path
ROOT=Path('/root/autodl-tmp/solo_conquest_A_forest_v3')
WORLDS=Path('/root/autodl-tmp/solo_world1200_v2_preserved_splits')
OUT=Path('/root/mageknight_solo_conquest_A_forest100_20261022')
STATUS=Path('/root/solo_conquest_launch_status.json')
Path('/root/solo_conquest_launch.lock').open('x').write(str(os.getpid()))
def status(**kw):STATUS.write_text(json.dumps(dict(pid=os.getpid(),time=time.time(),run=str(OUT),**kw)))
sys.path.insert(0,str(ROOT/'tools/BattleRl'))
from solo_world_manifest import read_tasks
from solo_training_admission import verify_admission
probe=json.loads((ROOT/'deployment_probe.json').read_text())
admission=verify_admission(ROOT,dict(binding_sha256=probe['native_binding']))
assert admission['record']['candidate_batching_passed']
proc=Path('/proc/731259');start=None
if proc.exists():
    assert b'run_solo_world_pair_dispatch.py' in (proc/'cmdline').read_bytes()
    start=(proc/'stat').read_text().split(') ',1)[1].split()[19]
while not (WORLDS/'index.json').exists():
    try:alive=start is not None and (proc/'stat').read_text().split(') ',1)[1].split()[19]==start
    except FileNotFoundError:alive=False
    if not alive:status(state='world_migration_failed_or_incomplete');sys.exit(1)
    status(state='waiting_verified_world_migration');time.sleep(20)
migration=json.loads((WORLDS/'migration.json').read_text())
assert migration['passed'] and migration['worlds']==1200 and migration['inventories_identical'] and migration['split_membership_preserved']
assert migration['new_bindings']==[probe['native_binding']]
assert len(read_tasks(WORLDS,'train'))==7000 and len(read_tasks(WORLDS,'validation'))==700
index=json.loads((WORLDS/'index.json').read_text());assert index['files']['test']['episodes']==700
if shutil.disk_usage('/root').free<3.5*1024**3:status(state='insufficient_disk_before_launch');sys.exit(1)
env=dict(os.environ,PATH='/root/autodl-tmp/dotnet:'+os.environ['PATH'],OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',MKL_NUM_THREADS='1')
common=['--repo',str(ROOT),'--dll',str(ROOT/'native/SoloConquestRl.dll'),'--worlds',str(WORLDS),
    '--schema',str(ROOT/'public_dto_schema.json'),'--assets',str(ROOT/'text_assets')]
with Path(str(OUT)+'.train.log').open('x') as log:
    trainer=subprocess.Popen([sys.executable,str(ROOT/'tools/BattleRl/train_solo_conquest.py'),*common,
        '--out',str(OUT),'--updates','100','--candidate-batching'],env=env,stdout=log,stderr=subprocess.STDOUT)
status(state='trainer_starting',trainer_pid=trainer.pid)
while not (OUT/'evaluation_requests/0000.json').exists():
    if trainer.poll() is not None:status(state='trainer_failed_before_eval_request',trainer_returncode=trainer.returncode);sys.exit(1)
    time.sleep(2)
with Path(str(OUT)+'.eval.log').open('x') as log:
    evaluator=subprocess.Popen([sys.executable,str(ROOT/'tools/BattleRl/evaluate_solo_conquest.py'),*common,
        '--run',str(OUT),'--watch'],env=env,stdout=log,stderr=subprocess.STDOUT)
while trainer.poll() is None or evaluator.poll() is None:
    status(state='running' if trainer.poll() is None else 'training_stopped_evaluation_pending',trainer_pid=trainer.pid,
        evaluator_pid=evaluator.pid,trainer_returncode=trainer.poll(),evaluator_returncode=evaluator.poll())
    if evaluator.poll() not in (None,0):
        status(state='evaluation_failed_training_may_continue',trainer_pid=trainer.pid,evaluator_pid=evaluator.pid,
            evaluator_returncode=evaluator.returncode);trainer.wait();break
    time.sleep(20)
status(state='processes_finished_requires_results_audit',trainer_returncode=trainer.poll(),evaluator_returncode=evaluator.poll())
