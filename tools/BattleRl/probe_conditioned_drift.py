"""CPU-only readout distribution audit; no encoder, optimizer, or run mutations."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,io,json,hashlib,sqlite3
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import numpy as np
import torch
import train_sb3_atomic_entity_text as base
from conditioned_context_policy import ConditionedContextPolicy
from rollout_archive import load
torch.set_num_threads(1)
root=Path('remote_conditioned300')
raw=(root/'checkpoint.zip').read_bytes()
m=base.RecordedRecurrentPPO.load(io.BytesIO(raw),device='cpu')
m.policy.set_training_mode(False)
# Only completed, immutable archives; never use a partly collected next batch.
archives=sorted(root.glob('rollout_*.archive'))
db=sqlite3.connect('file:'+str(root/'contexts/packed_vectors/offsets.sqlite')+'?mode=ro',uri=True)
records=[]
with (root/'contexts/packed_vectors/vectors.bin').open('rb') as stream,torch.no_grad():
 for archive in archives[-3:]:
  b=load(archive)
  ids=list(dict.fromkeys(int(i) for i in b.observations['context_id'].reshape(-1) if i))
  # Uniformly cover chronological positions rather than only the first episodes.
  ids=[ids[i] for i in np.linspace(0,len(ids)-1,min(256,len(ids)),dtype=int)]
  outputs={'state':[],'value':[]}
  for ctx in ids:
   row=db.execute('SELECT metadata FROM entries WHERE context=? ORDER BY node LIMIT 1',(ctx,)).fetchone()
   if row is None:raise RuntimeError('Missing immutable root cache')
   meta=json.loads(row[0]);stream.seek(meta['offset']);payload=stream.read(meta['bytes'])
   assert hashlib.sha256(payload).hexdigest()==meta['sha256']
   v=torch.from_numpy(np.frombuffer(payload,dtype='<f4').reshape(meta['shape']).copy()[1:])
   for name,attention,seq in [('state',m.policy.features_extractor.state_attention,m.policy.features_extractor.state_adapter),('value',m.policy.value_attention,m.policy.token_value)]:
    pooled=(attention(v).softmax(0)*v).sum(0)
    outputs[name].append(seq[0](pooled))
  result={}
  for name,rows in outputs.items():
   x=torch.stack(rows);assert torch.isfinite(x).all()
   result[name]=dict(mean_absolute_coordinate_mean=float(x.mean(0).abs().mean()),rms=float(x.square().mean().sqrt()),absolute_p99=float(torch.quantile(x.abs().flatten(),.99)),absolute_max=float(x.abs().max()),fraction_abs_gt10=float((x.abs()>10).float().mean()))
  records.append(dict(archive=str(archive),contexts=len(ids),branches=result))
db.close()
report=dict(checkpoint_update=m.completed_updates,checkpoint_sha=hashlib.sha256(raw).hexdigest(),records=records,scope='Read-only CPU cached-text audit. Current checkpoint applied to recent completed training archives; descriptive distribution drift, not held-out learning or policy improvement.')
dest=Path('optimization_stage_20261014')/f'conditioned_drift_{m.completed_updates:04d}.json'
dest.write_text(json.dumps(report,indent=2));print(json.dumps(report))
