"""Gate-bound switch after the parent's complete fixed evaluation; no duplicate run."""
import os,sys,json,time,hashlib,shutil,zipfile,runpy,tarfile
from pathlib import Path
import psutil,torch
stage=Path('optimization_stage_20261014');source=Path('remote_tokenvalue300');out=Path('remote_entropy300');snapshot=stage/'deployment_snapshot';status=stage/'deployment_status.json'
def write(**kw):status.write_text(json.dumps(kw,indent=2))
assert not out.exists() and not snapshot.exists()
gate=json.loads(Path('remote_entropy_gate32/gate.json').read_text());assert gate['passed']
binding=json.loads(Path('remote_entropy_gate32/manifest.json').read_text())['migration']['stage_sha']
for name in ['entropy_token_value_policy.py','batched_token_value_policy.py','resume_entropy_token_value.py']:
 assert hashlib.sha256((stage/name).read_bytes()).hexdigest()==binding[name],name
p=psutil.Process(24446);created=p.create_time();assert 'deploy_token_value.py' in ' '.join(p.cmdline())
write(state='waiting_full_eval80',parent_pid=p.pid,created=created)
while True:
 assert p.is_running() and p.create_time()==created
 try:
  summary=json.loads((source/'eval_0080/summary.json').read_text())
  rows=[json.loads(s) for s in (source/'evaluation_metrics.jsonl').read_text().splitlines()]
  if summary['episodes']==1000 and summary['frozen_parameters'] and summary['training_rng_restored'] and any(r['update']==80 and r['episodes']==1000 for r in rows):break
 except (FileNotFoundError,json.JSONDecodeError):pass
 time.sleep(10)
paused=[];killed=False
try:
 for q in [p]+p.children(recursive=True):paused.append((q.pid,q.create_time()));q.suspend()
 state=torch.load(source/'continuation.pt',weights_only=False,map_location='cpu')
 with zipfile.ZipFile(source/'checkpoint.zip') as z:assert json.loads(z.read('data'))['completed_updates']==state['completed_updates']
 assert all(s['closed_episode'] for s in state['environments'])
 snapshot.mkdir()
 for name in ['checkpoint.zip','continuation.pt','manifest.json','atomic_supports.jsonl','contexts/contexts.jsonl','metrics.jsonl','evaluation_metrics.jsonl','performance.jsonl','inherited_quarantine.json','audit_quarantine.jsonl']:
  src=source/name
  if src.exists():(snapshot/name).parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(src,snapshot/name)
 (snapshot/'identity.json').write_text(json.dumps(dict(processes=paused,completed=state['completed_updates'],eval80_wins=summary['wins'])))
 for pid,identity in reversed(paused):
  try:
   q=psutil.Process(pid)
   if q.create_time()==identity:q.kill()
  except psutil.NoSuchProcess:pass
 killed=True
 write(state='starting',parent_completed=state['completed_updates'],eval80_wins=summary['wins'],pid=os.getpid())
finally:
 if not killed:
  for pid,identity in reversed(paused):
   try:
    q=psutil.Process(pid)
    if q.create_time()==identity:q.resume()
   except psutil.NoSuchProcess:pass
os.environ['PATH']='/root/autodl-tmp/dotnet:/root/autodl-tmp/mkvenv/bin:'+os.environ['PATH']
sys.path[:0]=[str(stage.resolve()),str(Path('optimization_stage_20261013').resolve()),str(Path('tools/BattleRl').resolve())]
import train_sb3_atomic_entity_text as base
original=base.RecordedRecurrentPPO.train
def train(model):
 original(model)
 write(state='training',pid=os.getpid(),completed=model.completed_updates,parent=str(snapshot))
 if model.completed_updates%20==0 or model.completed_updates==model.goal_updates:
  root=model.output;name=f'checkpoint_bundle_{model.completed_updates:04d}.tar.gz';dest=root/name
  with tarfile.open(str(dest)+'.pending','w:gz') as tar:
   for n in ['checkpoint.zip','continuation.pt','contexts/contexts.jsonl','atomic_supports.jsonl','manifest.json','metrics.jsonl','evaluation_metrics.jsonl']:
    if (root/n).exists():tar.add(root/n,arcname=n)
   for folder,names in [(stage,['entropy_token_value_policy.py','batched_token_value_policy.py','resume_entropy_token_value.py']),(Path('optimization_stage_20261013'),['token_value_policy.py','hierarchical_batch_runtime.py','hierarchical_batch_encoder.py','hierarchical_packed_cache.py'])]:
    for n in names:tar.add(folder/n,arcname='runtime/'+n)
  Path(str(dest)+'.pending').replace(dest)
  (root/'latest_checkpoint_bundle.json').write_text(json.dumps(dict(file=name,update=model.completed_updates,sha256=hashlib.sha256(dest.read_bytes()).hexdigest())))
base.RecordedRecurrentPPO.train=train
sys.argv=['resume_entropy_token_value.py','--parent',str(snapshot),'--out',str(out)]
runpy.run_path(str(stage/'resume_entropy_token_value.py'),run_name='__main__')
write(state='complete',pid=os.getpid())
