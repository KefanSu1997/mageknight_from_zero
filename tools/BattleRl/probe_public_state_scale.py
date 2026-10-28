"""Read-only CPU activation scale probe on a saved complete rollout."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,io,json,zipfile,hashlib
from pathlib import Path
sys.path.insert(0,'tools/BattleRl')
import torch
from rollout_archive import load
torch.set_num_threads(1)
root=Path('remote_public_state71');paths=list(root.glob('rollout_0075_*.archive'));assert len(paths)==1
b=load(paths[0]);x=torch.as_tensor(b.observations['state'].reshape(-1,6411),dtype=torch.float32)
raw=(root/'checkpoint.zip').read_bytes()
with zipfile.ZipFile(io.BytesIO(raw)) as z:state=torch.load(io.BytesIO(z.read('policy.pth')),map_location='cpu',weights_only=True)
p='features_extractor.public_state_residual.'
with torch.no_grad():
    pre=torch.nn.functional.linear(x,state[p+'0.weight'],state[p+'0.bias'])
    hidden=pre.tanh();output=torch.nn.functional.linear(hidden,state[p+'2.weight'],state[p+'2.bias'])
assert all(torch.isfinite(v).all() for v in (x,pre,hidden,output))
def stats(v):
    v=v.abs().flatten();return dict(abs_quantiles=torch.quantile(v,torch.tensor([.5,.95,.99,1.])).tolist(),rms=float(v.square().mean().sqrt()))
r=dict(checkpoint_sha=hashlib.sha256(raw).hexdigest(),archive=str(paths[0]),samples=len(x),input=stats(x),pre_tanh=stats(pre),tanh_abs_above_099=float((hidden.abs()>.99).float().mean()),output=stats(output),scope='Saved numeric residual only, no encoder inference/optimizer/environment; saturation diagnostic, not reward evidence.')
target=root/('numeric_scale_'+r['checkpoint_sha'][:12]+'.json');target.write_text(json.dumps(r,indent=2));print(json.dumps(r))
