"""Bundle the stopped review boundary with model, RNG and context dependencies."""
import os,json,hashlib,tarfile
from pathlib import Path
import psutil,torch
root=Path('remote_public_state71');stage=Path('optimization_stage_20261014')
status=json.loads((root/'status.json').read_text())
assert status['state']=='review_boundary' and status['completed']==91
for p in psutil.process_iter(['pid','cmdline']):
 if p.pid==os.getpid():continue
 cmd=p.info['cmdline'] or []
 if cmd and 'python' in Path(cmd[0]).name:
  assert not any(Path(a).name=='resume_public_state71.py' for a in cmd[1:]),'Trainer must finish closing environments first'
state=torch.load(root/'continuation.pt',map_location='cpu',weights_only=False)
assert state['completed_updates']==91 and all(e['closed_episode'] for e in state['environments'])
del state
names=['checkpoint.zip','continuation.pt','contexts/contexts.jsonl','contexts/selection_paths.jsonl','atomic_supports.jsonl','manifest.json','metrics.jsonl','performance.jsonl','episode_batches.jsonl','gradient_groups.jsonl','status.json','continuation_loaded.json']
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
binding=json.loads((root/'manifest.json').read_text())
sources={name:sha(root/name) for name in names}
runtime={n:stage/n for n in binding['repair_runtime_sha']}
runtime.update({n:stage/n for n in binding['continuation']['source_sha']})
for name,value in binding['repair_runtime_sha'].items():assert sha(runtime[name])==value
for name,value in binding['continuation']['source_sha'].items():assert sha(runtime[name])==value
dest=root/'checkpoint_bundle_0091.tar.gz';assert not dest.exists()
with tarfile.open(str(dest)+'.pending','w:gz') as tar:
 for n in names:tar.add(root/n,arcname=n)
 for n,p in runtime.items():tar.add(p,arcname='runtime/'+n)
assert sources=={name:sha(root/name) for name in names},'Source changed during bundle'
Path(str(dest)+'.pending').replace(dest)
report=dict(file=dest.name,update=91,sha256=sha(dest),source_sha=sources,scope='Closed review boundary model/Adam/RNG, full context/support/path journal; trajectories remain in run directory and parent lineage is retained.')
(root/'latest_checkpoint_bundle.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
