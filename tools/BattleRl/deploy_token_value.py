import json,hashlib,sys,os,runpy,tarfile
from pathlib import Path
import psutil
root=Path('optimization_stage_20261013');gate=Path('remote_tokenvalue_gate32')
assert json.loads((gate/'gate.json').read_text())['passed']
manifest=json.loads((gate/'manifest.json').read_text())['migration']
for name in ['resume_token_value.py','token_value_policy.py','hierarchical_batch_runtime.py','hierarchical_batch_encoder.py','hierarchical_packed_cache.py']:
 assert hashlib.sha256((root/name).read_bytes()).hexdigest()==manifest['stage_sha'][name],name
identity=json.loads((root/'parent_snapshot/pause_identity.json').read_text())
# The stopped parent is superseded only after successful migration and real CUDA gate.
for row in identity['processes']:
 try:
  p=psutil.Process(row['pid'])
  if p.create_time()==row['created']:p.kill()
 except psutil.NoSuchProcess:pass
sys.path.insert(0,str(Path('tools/BattleRl').resolve()));sys.path.insert(0,str(root.resolve()))
import train_sb3_atomic_entity_text as base
old_train=base.RecordedRecurrentPPO.train
def train(model):
 old_train(model)
 if model.completed_updates%20==0 or model.completed_updates==model.goal_updates:
  out=model.output;name=f'checkpoint_bundle_{model.completed_updates:04d}.tar.gz';dest=out/name
  with tarfile.open(str(dest)+'.pending','w:gz') as f:
   for n in ['checkpoint.zip','continuation.pt','contexts/contexts.jsonl','atomic_supports.jsonl','manifest.json','metrics.jsonl','evaluation_metrics.jsonl']:
    if (out/n).exists():f.add(out/n,arcname=n)
   for n in ['token_value_policy.py','resume_token_value.py','hierarchical_batch_runtime.py','hierarchical_batch_encoder.py','hierarchical_packed_cache.py']:f.add(root/n,arcname='runtime/'+n)
  Path(str(dest)+'.pending').replace(dest)
  (out/'latest_checkpoint_bundle.json').write_text(json.dumps(dict(file=name,update=model.completed_updates,sha256=hashlib.sha256(dest.read_bytes()).hexdigest())))
base.RecordedRecurrentPPO.train=train
sys.argv=['resume_token_value.py','--parent',str(root/'parent_snapshot'),'--out','remote_tokenvalue300']
runpy.run_path(str(root/'resume_token_value.py'),run_name='__main__')
