import json,time,hashlib,shutil,zipfile,psutil
from pathlib import Path
import torch
root=Path('/root/autodl-tmp/mageknight');run=root/'remote_final_eval_resume300'
p=psutil.Process(239705);q=psutil.Process(243081)
assert 'run_final_eval_resume.py' in ' '.join(p.cmdline())
assert 'final_parallel_eval_worker.py' in ' '.join(q.cmdline())
assert not q.children(), 'Evaluator active: inspect separately'
identity={'trainer':{'pid':p.pid,'created':p.create_time(),'cmd':p.cmdline()},'queue':{'pid':q.pid,'created':q.create_time(),'cmd':q.cmdline()}}
q.terminate();q.wait(10)
for attempt in range(60):
 p.suspend()
 try:
  c=torch.load(run/'continuation.pt',map_location='cpu',weights_only=False)
  with zipfile.ZipFile(run/'checkpoint.zip') as z:d=json.loads(z.read('data'))
  completed=int(c['completed_updates']);valid=int(d['completed_updates'])==completed
 except Exception:valid=False
 if valid:break
 p.resume();time.sleep(2)
else:raise RuntimeError('No consistent checkpoint, trainer resumed')
try:
 dest=run/'user_stop_exploration';dest.mkdir(exist_ok=False);files={}
 for name in ['checkpoint.zip','continuation.pt','metrics.jsonl','manifest.json']:
  shutil.copy2(run/name,dest/name);files[name]=hashlib.sha256((dest/name).read_bytes()).hexdigest()
 children=p.children();assert all('BattleRl.dll' in ' '.join(x.cmdline()) for x in children)
 p.terminate();p.resume()
 for x in children:x.terminate()
 _,alive=psutil.wait_procs([p]+children,timeout=10)
 for x in alive:x.kill()
 report=dict(identity=identity,completed=completed,files=files,stopped=True,reason='User terminated current run; explore candidate-aware alternatives',time=time.time())
 (run/'user_stop_exploration.json').write_text(json.dumps(report,indent=2));print(json.dumps({'stopped':True,'completed':completed,'children':len(children)}))
except:
 if p.is_running():
  try:p.resume()
  except psutil.Error:pass
 raise
