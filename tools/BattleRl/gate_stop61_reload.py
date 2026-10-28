"""Read-only remote checkpoint/optimizer and continuation integrity gate."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,json,hashlib,io,zipfile,random
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import torch,numpy as np
import train_sb3_atomic_entity_text as base
torch.set_num_threads(1)
root=Path('remote_repaired_trainonly300');out=Path('optimization_stage_20261014/stop61_reload_gate.json')
assert not out.exists()
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
before={n:sha(root/n) for n in ('checkpoint.zip','continuation.pt','manifest.json')}
preserved=json.loads(Path('optimization_stage_20261014/stop61_preservation.json').read_text())
assert all(v==preserved['source_sha256'][k] for k,v in before.items())
manifest=json.loads((root/'manifest.json').read_text())
for n,digest in manifest['repair_runtime_sha'].items():assert sha(Path('optimization_stage_20261014')/n)==digest,n
m=base.RecordedRecurrentPPO.load(root/'checkpoint.zip',device='cpu');m.policy.set_training_mode(False)
state=torch.load(root/'continuation.pt',map_location='cpu',weights_only=False)
assert m.completed_updates==state['completed_updates']==61
def equal(a,b):
 if torch.is_tensor(a):return torch.equal(a.cpu(),b.cpu())
 if isinstance(a,np.ndarray):return np.array_equal(a,b)
 if isinstance(a,dict):return a.keys()==b.keys() and all(equal(a[k],b[k]) for k in a)
 if isinstance(a,(list,tuple)):return len(a)==len(b) and all(equal(x,y) for x,y in zip(a,b))
 return a==b
with zipfile.ZipFile(root/'checkpoint.zip') as z:
 policy=torch.load(io.BytesIO(z.read('policy.pth')),map_location='cpu',weights_only=False)
 adam=torch.load(io.BytesIO(z.read('policy.optimizer.pth')),map_location='cpu',weights_only=False)
assert equal(policy,m.policy.state_dict()) and equal(adam,m.policy.optimizer.state_dict())
assert all(torch.isfinite(t).all() for t in m.policy.state_dict().values())
assert all(torch.isfinite(v).all() for s in m.policy.optimizer.state.values() for v in s.values() if torch.is_tensor(v))
assert len(state['environments'])==32 and all(e['closed_episode'] for e in state['environments'])
assert all(torch.count_nonzero(t)==0 for pair in state['lstm'] for t in pair)
assert np.all(state['episode_starts']) and np.count_nonzero(state['last_obs']['context_id'])==0
def draw():return [random.random(),np.random.random(),torch.rand(4),torch.rand(4,device='cuda').cpu()]
# Finish lazy CUDA initialization before applying checkpoint RNG, as a CUDA
# model load does in the training restoration path.
torch.cuda.init();torch.empty(1,device='cuda');torch.cuda.synchronize()
base.restore_rng(state['rng']);first=draw();base.restore_rng(state['rng']);second=draw()
if not equal(first,second):
 failure=dict(saved_cuda_states=len(state['rng']['cuda']),equal_components=[equal(a,b) for a,b in zip(first,second)],cuda_initialized=torch.cuda.is_initialized())
 Path('optimization_stage_20261014/stop61_rng_first_failure.json').write_text(json.dumps(failure,indent=2));raise AssertionError(failure)
workers=[]
for e in state['environments']:
 rng=np.random.default_rng();rng.bit_generator.state=e['rng'];a=rng.integers(0,2**31,size=8)
 rng.bit_generator.state=e['rng'];assert np.array_equal(a,rng.integers(0,2**31,size=8))
 workers.append(dict(episode=e['episode'],total_steps=e['total_steps'],results=len(e['results'])))
assert before=={n:sha(root/n) for n in before}
report=dict(passed=True,completed=61,timesteps=m.num_timesteps,source_sha=before,parameter_sha=base.parameter_sha(m.policy),optimizer_states=len(adam['state']),adam_steps=sorted({float(s['step']) for s in adam['state'].values()}),all_state_dict_buffers_exact=True,optimizer_exact=True,global_rng_roundtrip=True,worker_rng_roundtrip=True,closed_environments=32,zero_lstm=True,workers=workers,scope='Actual model/Adam load and saved RNG roundtrip in isolated process. No training or source mutation. Native environment reconstruction and inference equivalence still required before resume.')
out.write_text(json.dumps(report,indent=2));print(json.dumps(report))
