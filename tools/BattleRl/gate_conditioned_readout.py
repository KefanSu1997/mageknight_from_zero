"""CPU algebra gate on actual saved text readout parameters and cached contexts."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,io,json,hashlib,sqlite3,copy
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import numpy as np,torch
from torch import nn
from stable_baselines3.common.save_util import load_from_zip_file
from rollout_archive import load
from conditioned_context_policy import install,fit_preserving_output
torch.set_num_threads(1)
root=Path('remote_context_pilot200');raw=(root/'checkpoint.zip').read_bytes()
data,params,_=load_from_zip_file(io.BytesIO(raw),device='cpu');state=params['policy']
archive=sorted(root.glob('rollout_*.archive'))[-1];b=load(archive)
ids=b.observations['context_id'].ravel().astype(int)[:512]
db=sqlite3.connect('file:'+str(root/'contexts/packed_vectors/offsets.sqlite')+'?mode=ro',uri=True)
stream=(root/'contexts/packed_vectors/vectors.bin').open('rb');vectors=[]
for ctx in ids:
 row=json.loads(db.execute('SELECT metadata FROM entries WHERE context=? ORDER BY node LIMIT 1',(int(ctx),)).fetchone()[0]);stream.seek(row['offset'])
 vectors.append(torch.tensor(np.frombuffer(stream.read(row['bytes']),dtype='<f4').reshape(row['shape']).copy()[1:]))
results=[]
for attention_prefix,readout_prefix,activation,width in [('features_extractor.state_attention.','features_extractor.state_adapter.',nn.GELU(),128),('value_attention.','token_value.',nn.Tanh(),1)]:
 att=nn.Sequential(nn.LayerNorm(384),nn.Linear(384,1,bias=False))
 net=nn.Sequential(nn.LayerNorm(384),nn.Linear(384,128),activation,nn.Linear(128,width))
 for module,prefix in [(att,attention_prefix),(net,readout_prefix)]:module.load_state_dict({k[len(prefix):]:v for k,v in state.items() if k.startswith(prefix)})
 with torch.no_grad():pooled=torch.stack([(att(v).softmax(0)*v).sum(0) for v in vectors])
 old=copy.deepcopy(net);install(net);fit_preserving_output(net,pooled[:384])
 with torch.no_grad():
  errors={}
  for name,x in [('train',pooled[:384]),('heldout_contexts',pooled[384:]),('random_input',torch.randn(128,384))]:
   errors[name]=float((old(x)-net(x)).abs().max())
  assert max(errors.values())<1e-5,errors
 net(pooled[384:]).square().mean().backward()
 assert all(p.grad is not None and torch.isfinite(p.grad).all() for p in net.parameters())
 stats=dict(center=net[0].center.tolist(),scale=net[0].scale.tolist())
 results.append(dict(readout=readout_prefix,max_absolute_errors=errors,gradients_finite=True,stats=stats))
report=dict(checkpoint_update=data['completed_updates'],checkpoint_sha=hashlib.sha256(raw).hexdigest(),archive=str(archive),fit_contexts=384,heldout_contexts=128,results=results,scope='Readout reparameterization only. Full recurrent policy, Adam migration, CUDA update, save/reload and fixed evaluation gates still required before deployment.')
out=Path('optimization_stage_20261014/conditioned_readout_gate.json');out.write_text(json.dumps(report,indent=2))
print(json.dumps({k:v for k,v in report.items() if k!='results'}));print([(r['readout'],r['max_absolute_errors']) for r in results])
