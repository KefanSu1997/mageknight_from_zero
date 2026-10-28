"""One-shot formal handoff after actual CUDA admission and reclaimed capacity."""
import hashlib,json,os,shutil,subprocess,time,zipfile
from pathlib import Path
ROOT=Path('/root/autodl-tmp')
SOURCE=Path('/root/solo_A_aligned_runtime_20261022')
REPO=ROOT/'solo_A_formal_runtime_20261022'
OUT=ROOT/'solo_A_aligned_fresh100_20261022'
GATE=Path('/root/solo_A_aligned_cuda_gate_20261022')
STATUS=Path('/root/solo_A_formal_launch_status.json')
PY='/root/autodl-tmp/mkvenv/bin/python'
def state(**v):STATUS.write_text(json.dumps(dict(time=time.time(),**v)))
def run():
    lock=Path('/root/solo_A_formal_launch.lock')
    with lock.open('x') as f:json.dump(dict(pid=os.getpid(),time=time.time()),f)
    while not (GATE/'recovered_result.json').exists():
        recovery=Path('/root/solo_A_gate_recovery_status.json')
        if recovery.exists() and json.loads(recovery.read_text()).get('state')=='failed':raise RuntimeError('Recovery gate failed; inspect evidence')
        state(state='waiting_actual_cuda_result');time.sleep(15)
    result=json.loads((GATE/'recovered_result.json').read_text())
    assert result['passed'] and result['reload_exact']
    while shutil.disk_usage(ROOT).free<28*2**30:
        state(state='waiting_28GiB_for_train_and_fixed700',free_bytes=shutil.disk_usage(ROOT).free);time.sleep(20)
    assert not REPO.exists() and not OUT.exists()
    shutil.copytree(SOURCE,REPO)
    with zipfile.ZipFile('/root/solo_A_formal_patch_20261022.zip') as z:
        for n in z.namelist():assert not Path(n).is_absolute() and '..' not in Path(n).parts
        z.extractall(REPO)
    manifest=json.loads((REPO/'formal_patch_manifest.json').read_text())
    for name,digest in manifest.items():assert hashlib.sha256((REPO/name).read_bytes()).hexdigest()==digest,name
    env=os.environ.copy();env.update(PATH='/root/autodl-tmp/dotnet:'+env['PATH'],OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',SOLO_BASELINE_A_OBSERVATION='1')
    with Path('/root/solo_A_formal_admission.log').open('w') as f:
        subprocess.run([PY,str(REPO/'tools/BattleRl/admit_baseline_a_solo.py'),'--repo',str(REPO),'--gate',str(GATE),
            '--engineering-proofs',str(REPO/'engineering_equivalence.json')],cwd=REPO,env=env,stdout=f,stderr=subprocess.STDOUT,check=True)
    common=['--repo',str(REPO),'--entity-assets','/root/autodl-tmp/mageknight/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets',
        '--worlds','/root/autodl-tmp/solo_world1200_v6_preserved_splits']
    log=Path('/root/solo_A_formal_train.log').open('w')
    trainer=subprocess.Popen([PY,'-u',str(REPO/'tools/BattleRl/train_baseline_a_solo.py'),*common,'--out',str(OUT),'--updates','100'],cwd=REPO,env=env,stdout=log,stderr=subprocess.STDOUT,start_new_session=True)
    while not (OUT/'evaluation_requests/0000.json').exists():
        if trainer.poll() is not None:raise RuntimeError('Trainer exited before initial evaluation request')
        state(state='trainer_initializing',trainer_pid=trainer.pid,out=str(OUT));time.sleep(2)
    elog=Path('/root/solo_A_formal_eval.log').open('w')
    evaluator=subprocess.Popen([PY,'-u',str(REPO/'tools/BattleRl/evaluate_baseline_a_solo.py'),*common,'--run',str(OUT)],cwd=REPO,env=env,stdout=elog,stderr=subprocess.STDOUT,start_new_session=True)
    state(state='train_and_independent_eval_started',trainer_pid=trainer.pid,evaluator_pid=evaluator.pid,
        out=str(OUT),repo=str(REPO),workers_each=32)
if __name__=='__main__':
    try:run()
    except BaseException:
        import traceback
        state(state='failed',traceback=traceback.format_exc());raise
