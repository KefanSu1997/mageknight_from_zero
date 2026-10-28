"""Read-only metadata consistency and SHA evidence after a resource stop."""
import hashlib,json,zipfile
from pathlib import Path
import torch
root=Path('/root/autodl-tmp/mageknight/remote_final_semantic_fast300')
state=torch.load(root/'continuation.pt',map_location='cpu',weights_only=False)
last=json.loads((root/'metrics.jsonl').read_text().splitlines()[-1])
with zipfile.ZipFile(root/'checkpoint.zip') as archive:
    data=json.loads(archive.read('data'))
assert state['completed_updates']==data['completed_updates']==last['update']
assert data['num_timesteps']==last['timesteps']
if 'num_timesteps' in state:
    assert state['num_timesteps']==last['timesteps']
files={}
for name in ['checkpoint.zip','continuation.pt','manifest.json','metrics.jsonl',
             'atomic_supports.jsonl','contexts/contexts.jsonl','episode_batches.jsonl','performance.jsonl']:
    path=root/name;h=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(1048576),b''):h.update(block)
    files[name]=dict(bytes=path.stat().st_size,sha256=h.hexdigest())
report=dict(completed=last['update'],timesteps=last['timesteps'],metadata_consistent=True,
            files=files,scope='Saved model/continuation metadata matches last complete update. Full archive is separate; no optimizer/RNG/native restoration gate claimed.')
# Outside the immutable run archive source, so archival inventory stays stable.
out=Path('/root/autodl-tmp/mageknight/optimization_stage_20261014/final_stop_preservation.json')
out.write_text(json.dumps(report,indent=2));print(json.dumps(report))
