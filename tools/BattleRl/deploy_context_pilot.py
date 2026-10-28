"""Resume the verified 181 snapshot with contextual recurrent state through eval200."""
import os,sys,json,hashlib,runpy,tarfile
from pathlib import Path
stage=Path('optimization_stage_20261014');gate=Path('remote_context_gate32_retry');out=Path('remote_context_pilot200')
assert json.loads((gate/'gate.json').read_text())['passed']
binding=json.loads((gate/'manifest.json').read_text())['migration']['stage_sha']
for name in ['context_state_policy.py','resume_context_state.py','entropy_token_value_policy.py','batched_token_value_policy.py']:
 assert hashlib.sha256((stage/name).read_bytes()).hexdigest()==binding[name],name
assert not out.exists()
lock=os.open(stage/'context_deployment.lock',os.O_CREAT|os.O_EXCL|os.O_WRONLY);os.write(lock,str(os.getpid()).encode());os.close(lock)
status=stage/'context_deployment_status.json'
def write(**kw):status.write_text(json.dumps(kw,indent=2))
write(state='initializing',pid=os.getpid(),target=200,parent_completed=181,scope='Pilot through fixed eval200; full learning objective remains open')
os.environ['PATH']='/root/autodl-tmp/dotnet:/root/autodl-tmp/mkvenv/bin:'+os.environ['PATH']
sys.path[:0]=[str(stage.resolve()),str(Path('optimization_stage_20261013').resolve()),str(Path('tools/BattleRl').resolve())]
import train_sb3_atomic_entity_text as base
original=base.RecordedRecurrentPPO.train
def train(model):
 original(model);write(state='training',pid=os.getpid(),completed=model.completed_updates,target=200)
 if model.completed_updates%20==0 or model.completed_updates==model.goal_updates:
  root=model.output;name=f'checkpoint_bundle_{model.completed_updates:04d}.tar.gz';dest=root/name
  with tarfile.open(str(dest)+'.pending','w:gz') as tar:
   for n in ['checkpoint.zip','continuation.pt','contexts/contexts.jsonl','atomic_supports.jsonl','manifest.json','metrics.jsonl','evaluation_metrics.jsonl']:
    if (root/n).exists():tar.add(root/n,arcname=n)
   for folder,names in [(stage,['context_state_policy.py','resume_context_state.py','entropy_token_value_policy.py','batched_token_value_policy.py']),(Path('optimization_stage_20261013'),['token_value_policy.py','hierarchical_batch_runtime.py','hierarchical_batch_encoder.py','hierarchical_packed_cache.py'])]:
    for n in names:tar.add(folder/n,arcname='runtime/'+n)
  Path(str(dest)+'.pending').replace(dest)
  (root/'latest_checkpoint_bundle.json').write_text(json.dumps(dict(file=name,update=model.completed_updates,sha256=hashlib.sha256(dest.read_bytes()).hexdigest())))
base.RecordedRecurrentPPO.train=train
sys.argv=['resume_context_state.py','--parent',str(stage/'context_parent_snapshot'),'--out',str(out),'--goal','200']
try:runpy.run_path(str(stage/'resume_context_state.py'),run_name='__main__')
except BaseException as error:
 write(state='failed',pid=os.getpid(),error=repr(error));raise
write(state='pilot_complete',pid=os.getpid(),target=200,learning_objective_complete=False)
