"""Bounded full128 continuation from220; independent gate221 is not inherited."""
import hashlib,json,os,runpy,sys,tarfile
from pathlib import Path
import psutil
stage=Path('optimization_stage_20261014');out=Path('remote_root_entropy240');parent=Path('remote_conditioned300')
assert not out.exists()
previous=json.loads((stage/'root_entropy_gate_status.json').read_text())
for pid in (122535,previous['gate_pid']):
    if psutil.pid_exists(pid):
        p=psutil.Process(pid)
        assert p.status()==psutil.STATUS_ZOMBIE,'Prior job still active'
gate=json.loads(Path('remote_root_entropy_gate32/gate.json').read_text())
assert gate['passed'] and gate['real_games']==32 and gate['reload_all_buffers_equal'] and gate['finite'] and gate['eval1_3_equal']
for n,digest in previous['preserved_sha'].items():assert hashlib.sha256((parent/n).read_bytes()).hexdigest()==digest,n
binding=json.loads(Path('remote_root_entropy_gate32/manifest.json').read_text())['migration']['stage_sha']
names=['root_entropy_conditioned_policy.py','conditioned_context_policy.py','context_state_policy.py','entropy_token_value_policy.py','batched_token_value_policy.py']
for n in names:assert hashlib.sha256((stage/n).read_bytes()).hexdigest()==binding[n],n
status=stage/'root_entropy_deployment_status.json'
def write(**kw):status.write_text(json.dumps(kw,indent=2))
os.environ['PATH']='/root/autodl-tmp/dotnet:/root/autodl-tmp/mkvenv/bin:'+os.environ['PATH']
os.environ['OPENBLAS_NUM_THREADS']='1'
sys.path[:0]=[str(stage.resolve()),str(Path('optimization_stage_20261013').resolve()),str(Path('tools/BattleRl').resolve())]
try:
    write(state='initializing',pid=os.getpid(),parent_completed=220,target=240,learning_improvement_verified=False)
    import train_sb3_atomic_entity_text as base
    original=base.RecordedRecurrentPPO.train
    def train(model):
        original(model)
        write(state='training',pid=os.getpid(),completed=model.completed_updates,target=240)
        if model.completed_updates%20==0:
            dest=out/f'checkpoint_bundle_{model.completed_updates:04d}.tar.gz'
            with tarfile.open(str(dest)+'.pending','w:gz') as tar:
                for n in ['checkpoint.zip','continuation.pt','contexts/contexts.jsonl','atomic_supports.jsonl','manifest.json','metrics.jsonl','evaluation_metrics.jsonl']:
                    if (out/n).exists():tar.add(out/n,arcname=n)
                for n in names+['resume_root_entropy_context.py']:tar.add(stage/n,arcname='runtime/'+n)
            Path(str(dest)+'.pending').replace(dest)
            (out/'latest_checkpoint_bundle.json').write_text(json.dumps(dict(file=dest.name,update=model.completed_updates,sha256=hashlib.sha256(dest.read_bytes()).hexdigest())))
    base.RecordedRecurrentPPO.train=train
    sys.argv=['resume_root_entropy_context.py','--parent',str(parent),'--out',str(out),'--goal','240']
    runpy.run_path(str(stage/'resume_root_entropy_context.py'),run_name='__main__')
    write(state='pilot_complete',pid=os.getpid(),completed=240,target=240,learning_objective_complete=False)
except BaseException as error:
    write(state='failed',pid=os.getpid(),error=repr(error));raise
