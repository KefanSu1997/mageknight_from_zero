"""Continue from real 200 only after the independent conditioned CUDA gate."""
import os,sys,json,hashlib,runpy,tarfile,time
from pathlib import Path
import psutil
stage=Path('optimization_stage_20261014');gate=Path('remote_conditioned_gate32')
parent=Path('remote_context_pilot200');out=Path('remote_conditioned300')
assert not out.exists()
lock=os.open(stage/'conditioned_deployment_v2.lock',os.O_CREAT|os.O_EXCL|os.O_WRONLY)
os.write(lock,str(os.getpid()).encode());os.close(lock)
status=stage/'conditioned_deployment_status.json'
def write(**kw):status.write_text(json.dumps(kw,indent=2))
try:
 waiter=psutil.Process(122341);assert 'optimization_stage_20261014/queue_conditioned_gate_v2.py' in waiter.cmdline()
 identity=dict(pid=waiter.pid,created=waiter.create_time(),command=waiter.cmdline())
 write(state='waiting_gate',pid=os.getpid(),gate_queue=identity,target=300)
 while True:
  try:
   if not waiter.is_running() or waiter.create_time()!=identity['created'] or waiter.status()==psutil.STATUS_ZOMBIE:break
  except psutil.NoSuchProcess:break
  time.sleep(30)
 queued=json.loads((stage/'conditioned_gate_queue_status.json').read_text());assert queued['state']=='gate_passed_not_deployed',queued
 assert json.loads((parent/'complete.json').read_text())['updates']==200
 result=json.loads((gate/'gate.json').read_text())
 assert result['passed'] and result['reload_all_buffers_equal']
 assert len(result['conditioned_parameter_deltas'])==2 and all(v>0 for v in result['conditioned_parameter_deltas'].values())
 assert all(v>0 for v in result['conditioned_adam_steps'].values())
 binding=json.loads((gate/'manifest.json').read_text())['migration']['stage_sha']
 names=['conditioned_context_policy.py','resume_conditioned_context.py','context_state_policy.py','entropy_token_value_policy.py','batched_token_value_policy.py']
 for name in names:assert hashlib.sha256((stage/name).read_bytes()).hexdigest()==binding[name],name
 write(state='initializing',pid=os.getpid(),target=300,parent_completed=200,learning_improvement_verified=False)
 os.environ['PATH']='/root/autodl-tmp/dotnet:/root/autodl-tmp/mkvenv/bin:'+os.environ['PATH']
 sys.path[:0]=[str(stage.resolve()),str(Path('optimization_stage_20261013').resolve()),str(Path('tools/BattleRl').resolve())]
 import train_sb3_atomic_entity_text as base
 original=base.RecordedRecurrentPPO.train
 def train(model):
  original(model);write(state='training',pid=os.getpid(),completed=model.completed_updates,target=300)
  if model.completed_updates%20==0 or model.completed_updates==model.goal_updates:
   root=model.output;name=f'checkpoint_bundle_{model.completed_updates:04d}.tar.gz';dest=root/name
   with tarfile.open(str(dest)+'.pending','w:gz') as tar:
    for n in ['checkpoint.zip','continuation.pt','contexts/contexts.jsonl','atomic_supports.jsonl','manifest.json','metrics.jsonl','evaluation_metrics.jsonl']:
     if (root/n).exists():tar.add(root/n,arcname=n)
    for folder,files in [(stage,names),(Path('optimization_stage_20261013'),['token_value_policy.py','hierarchical_batch_runtime.py','hierarchical_batch_encoder.py','hierarchical_packed_cache.py'])]:
     for n in files:tar.add(folder/n,arcname='runtime/'+n)
   Path(str(dest)+'.pending').replace(dest)
   (root/'latest_checkpoint_bundle.json').write_text(json.dumps(dict(file=name,update=model.completed_updates,sha256=hashlib.sha256(dest.read_bytes()).hexdigest())))
 base.RecordedRecurrentPPO.train=train
 sys.argv=['resume_conditioned_context.py','--parent',str(parent),'--out',str(out),'--goal','300']
 runpy.run_path(str(stage/'resume_conditioned_context.py'),run_name='__main__')
 write(state='training_complete',pid=os.getpid(),target=300,learning_objective_complete=False)
except BaseException as error:
 write(state='failed',pid=os.getpid(),error=repr(error));raise

