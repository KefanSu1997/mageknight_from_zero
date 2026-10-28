import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,json,sqlite3
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import numpy as np,torch
from torch import nn
from stable_baselines3.common.save_util import load_from_zip_file
from rollout_archive import load
torch.set_num_threads(1);root=Path('remote_context_pilot200')
data,params,_=load_from_zip_file(root/'checkpoint.zip',device='cpu');state=params['policy']
att=nn.Sequential(nn.LayerNorm(384),nn.Linear(384,1,bias=False));adapter=nn.Sequential(nn.LayerNorm(384),nn.Linear(384,128),nn.GELU(),nn.Linear(128,128));project=nn.Sequential(nn.LayerNorm(384),nn.Linear(384,128),nn.Tanh())
for name,module in [('state_attention',att),('state_adapter',adapter),('projection',project)]:
 prefix='features_extractor.'+name+'.';module.load_state_dict({k[len(prefix):]:v for k,v in state.items() if k.startswith(prefix)})
b=load(sorted(root.glob('rollout_*.archive'))[-1]);ids=b.observations['context_id'].ravel().astype(int)[:512]
db=sqlite3.connect('file:'+str(root/'contexts/packed_vectors/offsets.sqlite')+'?mode=ro',uri=True);stream=(root/'contexts/packed_vectors/vectors.bin').open('rb');base=[];delta=[]
with torch.no_grad():
 for ctx in ids:
  row=json.loads(db.execute('SELECT metadata FROM entries WHERE context=? ORDER BY node LIMIT 1',(int(ctx),)).fetchone()[0]);stream.seek(row['offset']);v=torch.tensor(np.frombuffer(stream.read(row['bytes']),dtype='<f4').reshape(row['shape']).copy())
  base.append(project(v[0]));weights=att(v[1:]).softmax(0);delta.append(adapter((weights*v[1:]).sum(0)))
 base=torch.stack(base);delta=torch.stack(delta)
report=dict(checkpoint_update=data['completed_updates'],samples=len(ids),base_mean_norm=float(base.norm(dim=1).mean()),new_branch_mean_norm=float(delta.norm(dim=1).mean()),base_coordinate_std=float(base.std(0).mean()),combined_coordinate_std=float((base+delta).std(0).mean()),new_branch_nonzero_parameters=int(torch.count_nonzero(adapter[-1].weight)),scope='Mechanism diagnostic on real archived contexts; does not prove ability improvement.')
def variability(x):
 centered=x-x.mean(0)
 return dict(mean_norm=float(x.mean(0).norm()),centered_rms_norm=float(centered.square().sum(1).mean().sqrt()),coordinate_std=float(x.std(0).mean()),constant_energy_fraction=float(x.mean(0).square().sum()/x.square().sum(1).mean().clamp_min(1e-20)))
report['base_variability']=variability(base)
report['branch_variability']=variability(delta)
report['combined_variability']=variability(base+delta)
report['archive']=str(sorted(root.glob('rollout_*.archive'))[-1])
report['warning']='Feature magnitude and nonzero weights alone do not show state discrimination. Constant energy includes the batch mean; sampled contexts may repeat.'
Path(f"optimization_stage_20261014/context_branch_effect_{data['completed_updates']:04d}.json").write_text(json.dumps(report,indent=2))
Path('optimization_stage_20261014/context_branch_effect.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
