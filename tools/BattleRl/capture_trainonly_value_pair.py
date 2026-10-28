"""Capture immutable train-only checkpoints for prospective value diagnosis."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,io,json,hashlib,tarfile
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import torch
import train_sb3_atomic_entity_text as base
from root_entropy_conditioned_policy import RootEntropyConditionedPolicy
torch.set_num_threads(1)
root=Path('remote_repaired_trainonly300');out=Path('optimization_stage_20261014/trainonly_value_pair')
out.mkdir(exist_ok=False)
with tarfile.open(root/'checkpoint_bundle_0020.tar.gz') as bundle:
    older=bundle.extractfile('checkpoint.zip').read()
newer=(root/'checkpoint.zip').read_bytes()
rows=[]
for label,raw in [('older',older),('newer',newer)]:
    model=base.RecordedRecurrentPPO.load(io.BytesIO(raw),device='cpu')
    assert model.evaluation_every==0
    path=out/(label+'.zip');path.write_bytes(raw)
    rows.append(dict(path=str(path),update=model.completed_updates,sha=hashlib.sha256(raw).hexdigest()))
assert rows[0]['update']==20 and rows[1]['update']>20
report=dict(checkpoints=rows,run=str(root),future_archive_update=rows[1]['update'],scope='Freeze both before inspecting subsequent complete training batch; no optimization, no fixed evaluation, no live mutation.')
(out/'binding.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
