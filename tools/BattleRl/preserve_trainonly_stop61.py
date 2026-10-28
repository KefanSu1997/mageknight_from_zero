"""Preserve the completed low-disk boundary, without modifying or restarting training."""
import json,zipfile,hashlib,tarfile,os
from pathlib import Path
import psutil
root=Path('remote_repaired_trainonly300');stage=Path('optimization_stage_20261014')
assert not psutil.pid_exists(141816)
status=json.loads((stage/'repaired_trainonly_deployment_status.json').read_text());assert 'Low disk reserve' in status['error']
metrics=json.loads((root/'metrics.jsonl').read_text().splitlines()[-1]);assert metrics['update']==61
with zipfile.ZipFile(root/'checkpoint.zip') as z:data=json.loads(z.read('data'))
assert data['completed_updates']==61 and data['num_timesteps']==metrics['timesteps']
names=['checkpoint.zip','continuation.pt','contexts/contexts.jsonl','contexts/selection_paths.jsonl','atomic_supports.jsonl','manifest.json','metrics.jsonl','performance.jsonl','episode_batches.jsonl','fresh_initialization.json']
def sha(p):
 h=hashlib.sha256()
 with p.open('rb') as f:
  for b in iter(lambda:f.read(1048576),b''):h.update(b)
 return h.hexdigest()
files={name:sha(root/name) for name in names}
report=dict(completed=61,timesteps=metrics['timesteps'],stop_reason=status['error'],source_sha256=files,scope='Checkpoint metadata matches completed metrics; full reload and continuation RNG gate still required before resume.')
report_path=stage/'stop61_preservation.json';report_path.write_text(json.dumps(report,indent=2))
dest=root/'checkpoint_bundle_stop0061.tar.gz';assert not dest.exists()
with tarfile.open(str(dest)+'.pending','w:gz') as tar:
 for name in names:tar.add(root/name,arcname=name)
 tar.add(report_path,arcname='stop61_preservation.json')
 binding=json.loads((root/'manifest.json').read_text())['repair_runtime_sha']
 for name,digest in binding.items():
  assert sha(stage/name)==digest
  tar.add(stage/name,arcname='runtime/'+name)
os.replace(str(dest)+'.pending',dest)
assert files=={name:sha(root/name) for name in names}
report.update(bundle=str(dest),bundle_sha256=sha(dest));report_path.write_text(json.dumps(report,indent=2));print(json.dumps(report))
