"""Start random fresh300 only after actual fresh32 CUDA acceptance."""
import hashlib,json,os,sys,runpy,tarfile
from pathlib import Path
import psutil
stage=Path('optimization_stage_20261014');gate=Path('remote_repaired_fresh_gate32_retry');out=Path('remote_repaired_fresh300')
assert not out.exists()
identity=json.loads((stage/'repaired_fresh_gate_status.json').read_text())
if psutil.pid_exists(identity['pid']):assert psutil.Process(identity['pid']).status()==psutil.STATUS_ZOMBIE
result=json.loads((gate/'gate.json').read_text())
assert result['passed'] and result['real_games']==32 and result['reload_all_buffers_equal'] and result['parallel_eval_equal']
initial=json.loads((gate/'fresh_initialization.json').read_text())
assert initial['optimizer_states']==0 and initial['completed_updates']==0 and not initial['previous_checkpoint_loaded']
binding=json.loads((gate/'manifest.json').read_text())['repair_runtime_sha']
for name,digest in binding.items():assert hashlib.sha256((stage/name).read_bytes()).hexdigest()==digest,name
os.environ.update(OPENBLAS_NUM_THREADS='1',BATTLE_RL_ENCODER_BATCH='4')
os.environ['PATH']='/root/autodl-tmp/dotnet:/root/autodl-tmp/mkvenv/bin:'+os.environ['PATH']
sys.path[:0]=[str(stage.resolve()),str(Path('optimization_stage_20261013').resolve()),str(Path('tools/BattleRl').resolve())]
status=stage/'repaired_fresh_deployment_status.json'
def write(**kw):status.write_text(json.dumps(kw,indent=2))
try:
    import train_sb3_atomic_entity_text as base
    import fresh_text_conditioning as conditioning
    original_fit=conditioning.fit_fresh_conditioning
    def checked_fit(model,output):
        original_fit(model,output)
        assert base.parameter_sha(model.policy)==initial['parameter_sha'],'Fresh initialization differs from gate'
        write(state='baseline',pid=os.getpid(),completed=0,target=300,optimizer_states=0,gate_initial_parameters_equal=True)
    conditioning.fit_fresh_conditioning=checked_fit
    original=base.RecordedRecurrentPPO.train
    def train(model):
        original(model)
        write(state='training',pid=os.getpid(),completed=model.completed_updates,target=300)
        if model.completed_updates%20==0 or model.completed_updates==model.goal_updates:
            dest=out/f'checkpoint_bundle_{model.completed_updates:04d}.tar.gz'
            with tarfile.open(str(dest)+'.pending','w:gz') as tar:
                for n in ['checkpoint.zip','continuation.pt','contexts/contexts.jsonl','atomic_supports.jsonl','manifest.json','metrics.jsonl','evaluation_metrics.jsonl']:
                    if (out/n).exists():tar.add(out/n,arcname=n)
                for n in binding:tar.add(stage/n,arcname='runtime/'+n)
            Path(str(dest)+'.pending').replace(dest)
            (out/'latest_checkpoint_bundle.json').write_text(json.dumps(dict(file=dest.name,update=model.completed_updates,sha256=hashlib.sha256(dest.read_bytes()).hexdigest())))
    base.RecordedRecurrentPPO.train=train
    write(state='initializing',pid=os.getpid(),completed=0,target=300)
    sys.argv=['train_repaired_fresh.py','--out',str(out),'--updates','300','--workers','32']
    runpy.run_path(str(stage/'train_repaired_fresh.py'),run_name='__main__')
    write(state='training_complete',pid=os.getpid(),completed=300,learning_objective_complete=False)
except BaseException as error:
    write(state='failed',pid=os.getpid(),error=repr(error));raise
