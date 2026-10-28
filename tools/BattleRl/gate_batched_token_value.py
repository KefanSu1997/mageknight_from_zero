"""CUDA forward/backward parity and paired timings on real cached contexts."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,json,time,sqlite3,copy
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import torch,numpy as np,psutil
from stable_baselines3.common.save_util import load_from_zip_file
from batched_token_value_policy import pooled_value
torch.set_num_threads(1)
out=Path('optimization_stage_20261014');root=Path('remote_tokenvalue300');paused=[]
try:
 p=psutil.Process(24446);assert 'deploy_token_value.py' in ' '.join(p.cmdline())
 for item in [p]+p.children(recursive=True):paused.append((item.pid,item.create_time()));item.suspend()
 _,params,_=load_from_zip_file(root/'checkpoint.zip',device='cuda');state=params['policy']
 att=torch.nn.Sequential(torch.nn.LayerNorm(384),torch.nn.Linear(384,1,bias=False)).cuda()
 val=torch.nn.Sequential(torch.nn.LayerNorm(384),torch.nn.Linear(384,128),torch.nn.Tanh(),torch.nn.Linear(128,1)).cuda()
 att.load_state_dict({k.removeprefix('value_attention.'):v for k,v in state.items() if k.startswith('value_attention.')})
 val.load_state_dict({k.removeprefix('token_value.'):v for k,v in state.items() if k.startswith('token_value.')})
 ids=np.load(out/'probe_samples.npz')['context'][:128]
 db=sqlite3.connect('file:'+str(root/'contexts/packed_vectors/offsets.sqlite')+'?mode=ro',uri=True);f=(root/'contexts/packed_vectors/vectors.bin').open('rb');arrays=[]
 for i in ids:
  r=json.loads(db.execute('SELECT metadata FROM entries WHERE context=? ORDER BY node LIMIT 1',(int(i),)).fetchone()[0]);f.seek(r['offset']);arrays.append(np.frombuffer(f.read(r['bytes']),dtype='<f4').reshape(r['shape'])[1:].copy())
 def old():
  values=[]
  for a in arrays:
   x=torch.as_tensor(a,device='cuda');w=att(x).softmax(0);values.append(val((w*x).sum(0,keepdim=True)))
  return torch.cat(values)
 def new():return pooled_value(att,val,arrays,'cuda')
 parameters=list(att.parameters())+list(val.parameters());a=old();a.square().mean().backward();grad=[p.grad.clone() for p in parameters]
 for p in parameters:p.grad=None
 b=new();b.square().mean().backward()
 output=float((a-b).abs().max());gradient=max(float((g-p.grad).abs().max()) for g,p in zip(grad,parameters))
 assert output<2e-5 and gradient<2e-5,(output,gradient)
 timings=[]
 for name,fn in [('old',old),('new',new),('new',new),('old',old)]:
  for _ in range(3):
   for p in parameters:p.grad=None
   fn().square().mean().backward()
  torch.cuda.synchronize();start=time.perf_counter()
  for _ in range(20):
   for p in parameters:p.grad=None
   fn().square().mean().backward()
  torch.cuda.synchronize();timings.append(dict(kind=name,seconds_per_batch=(time.perf_counter()-start)/20))
 report=dict(passed=True,contexts=len(arrays),output_max_error=output,gradient_max_error=gradient,timings=timings,scope='V residual branch only, real checkpoint and 128 cached contexts. Not whole PPO or sampling speedup; weights not updated.')
 (out/'batched_value_gate.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
finally:
 for pid,created in reversed(paused):
  try:
   p=psutil.Process(pid)
   if p.create_time()==created:p.resume()
  except psutil.NoSuchProcess:pass
