"""One-shot fresh100 launch after verified world migration; independent eval32."""
import json,os,shutil,subprocess,sys,time
from pathlib import Path
os.environ['PATH']='/root/autodl-tmp/dotnet:'+os.environ['PATH']
ROOT=Path('/root/autodl-tmp/solo_conquest_enemyshuffle_v6')
WORLDS=Path('/root/autodl-tmp/solo_world1200_v6_preserved_splits')
OUT=Path('/root/mageknight_solo_conquest_A_enemyshuffle_v6_100_20261022')
STATUS=Path('/root/solo_conquest_enemyshuffle_v6_launch_status.json')
Path('/root/solo_conquest_enemyshuffle_v6_launch_retry1.lock').open('x').write(str(os.getpid()))
def status(**kw):STATUS.write_text(json.dumps(dict(pid=os.getpid(),time=time.time(),run=str(OUT),**kw)))
sys.path.insert(0,str(ROOT/'tools/BattleRl'))
from solo_world_manifest import read_tasks
from solo_training_admission import verify_admission
probe=json.loads((ROOT/'deployment_probe.json').read_text())
gate=ROOT/'cuda_runtime_gate/result.json'
gate_proc=Path('/proc/906725');gate_start=None
if gate_proc.exists():
    assert b'run_solo_enemyshuffle_v6_gate.py' in (gate_proc/'cmdline').read_bytes()
    gate_start=(gate_proc/'stat').read_text().split(') ',1)[1].split()[19]
while not gate.exists():
    try:alive=gate_start is not None and (gate_proc/'stat').read_text().split(') ',1)[1].split()[19]==gate_start
    except FileNotFoundError:alive=False
    if not alive:status(state='cuda_gate_failed_requires_review');sys.exit(1)
    status(state='waiting_full_cuda_gate');time.sleep(20)
assert json.loads(gate.read_text())['passed']
if not (ROOT/'training_admission.json').exists():
    admitted=subprocess.run([sys.executable,'/root/autodl-tmp/admit_solo_enemyshuffle_v6.py'])
    if admitted.returncode:status(state='admission_failed_requires_review');sys.exit(admitted.returncode)
admission=verify_admission(ROOT,dict(binding_sha256=probe['native_binding']))
assert admission['record']['candidate_batching_passed']
from solo_native_env import SoloNativeEnv
with SoloNativeEnv(ROOT/'native/SoloConquestRl.dll',ROOT,ROOT/'formal_admission_probe_retry1') as native:
    assert native.capabilities['binding_sha256']==probe['native_binding']
    (ROOT/'formal_admission_verified.json').write_text(json.dumps(dict(passed=True,time=time.time(),native_binding=probe['native_binding'],admission_sha256=native.admission['sha256'])))
proc=Path('/proc/906367');start=None
if proc.exists():
    assert b'migrate_solo_world_enemyshuffle_v6.py' in (proc/'cmdline').read_bytes()
    start=(proc/'stat').read_text().split(') ',1)[1].split()[19]
while not (WORLDS/'index.json').exists():
    try:alive=start is not None and (proc/'stat').read_text().split(') ',1)[1].split()[19]==start
    except FileNotFoundError:alive=False
    if not alive:status(state='world_migration_failed_or_incomplete');sys.exit(1)
    status(state='waiting_verified_world_migration');time.sleep(20)
migration=json.loads((WORLDS/'migration.json').read_text())
assert migration['worlds_verified']==1200 and migration['hero_resets_verified']==8400
assert migration['split_membership_preserved'] and migration['enemy_shuffle_repair']
assert migration['native_binding']==probe['native_binding']
assert migration['counts']==dict(train=7000,validation=700,test=700)
assert len(read_tasks(WORLDS,'train'))==7000 and len(read_tasks(WORLDS,'validation'))==700
index=json.loads((WORLDS/'index.json').read_text());assert index['files']['test']['episodes']==700
if shutil.disk_usage('/root').free<3.5*1024**3:status(state='insufficient_disk_before_launch');sys.exit(1)
assert not OUT.exists(), 'Never overwrite or duplicate an existing run'
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


