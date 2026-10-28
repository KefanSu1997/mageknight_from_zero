"""Resume only the stopped, zero-update seven-game gate from persisted behavior."""
import hashlib,json,os,shutil,sqlite3,time,traceback
from pathlib import Path
os.chdir(Path(__file__).resolve().parents[2])
import numpy as np
import torch
from sb3_contrib import RecurrentPPO
from stable_baselines3.common.logger import configure
from baseline_a_scenario_runtime import ScenarioRuntime
from baseline_a_scenario_ppo import update_complete
from run_baseline_a_solo_cuda_gate import signature

OLD=Path('/root/solo_A_aligned_cuda_gate_20261022')
DEST=Path('/root/autodl-tmp/solo_A_aligned_cuda_gate_20261022')
STATUS=Path('/root/solo_A_gate_recovery_status.json')
def sha(p):
    h=hashlib.sha256()
    with p.open('rb') as f:
        for b in iter(lambda:f.read(8388608),b''):h.update(b)
    return h.hexdigest()
def state(**kw):STATUS.write_text(json.dumps(dict(time=time.time(),**kw)))
def run():
    assert os.name!='nt' and torch.cuda.is_available()
    assert '3GiB protection before PPO' in (OLD/'failure.txt').read_text()
    assert not (OLD/'checkpoint_0001.zip').exists()
    if not OLD.is_symlink():
        assert OLD.resolve()==OLD and not DEST.exists()
        while shutil.disk_usage(DEST.parent).free<4.5*2**30:
            state(state='waiting_data_space_4.5GiB');time.sleep(15)
        # Only a stopped experiment is moved; byte-for-byte verify before source removal.
        for p in Path('/proc').glob('[0-9]*/cmdline'):
            try:c=p.read_bytes()
            except (FileNotFoundError,PermissionError):continue
            assert str(OLD).encode() not in c, str(p)
        files={str(p.relative_to(OLD)):sha(p) for p in OLD.rglob('*') if p.is_file()}
        assert not any(p.is_symlink() for p in OLD.rglob('*'))
        shutil.copytree(OLD,DEST)
        assert {str(p.relative_to(DEST)):sha(p) for p in DEST.rglob('*') if p.is_file()}==files
        (DEST/'migration_sha.json').write_text(json.dumps(files))
        shutil.rmtree(OLD);OLD.symlink_to(DEST,target_is_directory=True)
    assert OLD.resolve()==DEST and shutil.disk_usage(DEST).free>=3*2**30
    state(state='loading_saved_seven_games')
    contract=json.loads((DEST/'contract.json').read_text())
    for name,digest in contract['sources'].items():assert sha(Path(__file__).parent/name)==digest,name
    db=sqlite3.connect(DEST/'contexts/scenario_contexts.sqlite')
    binding=json.loads(db.execute('SELECT payload FROM binding WHERE id=1').fetchone()[0]);db.close()
    runtime=ScenarioRuntime(DEST/'contexts',binding)
    try:
        torch.set_num_threads(1);torch.manual_seed(7112026);np.random.seed(7112026)
        model=RecurrentPPO.load(DEST/'initial_checkpoint.zip',device='cuda')
        assert not model.policy.optimizer.state
        model.set_logger(configure(format_strings=[]));before=signature(model.policy)
        episodes=[]
        for i in range(7):
            saved=torch.load(DEST/f'collection/episode_{i:06d}/behavior_context.pt',map_location='cpu',weights_only=False)
            assert saved['pending'] is None and saved['rows'][-1]['terminated']
            episodes.append(saved['rows'])
        state(state='actual_cuda_ppo',decisions=sum(map(len,episodes)))
        started=time.perf_counter();result=update_complete(model,episodes,runtime)
        after=signature(model.policy);assert before!=after
        model.save(DEST/'checkpoint_0001.zip')
        restored=RecurrentPPO.load(DEST/'checkpoint_0001.zip',device='cuda');assert signature(restored.policy)==after
        (DEST/'recovered_result.json').write_text(json.dumps(dict(passed=True,update=result,reload_exact=True,
            initial_sha=before,final_sha=after,update_save_reload_seconds=time.perf_counter()-started,
            collection_seconds=288.931976349093,scope='Seven actual games; zero-update checkpoint with explicit optimizer RNG reseed; not formal128 throughput'),indent=2))
        state(state='passed',result=str(DEST/'recovered_result.json'))
    finally:runtime.close()
if __name__=='__main__':
    try:run()
    except BaseException:
        state(state='failed',traceback=traceback.format_exc());raise
