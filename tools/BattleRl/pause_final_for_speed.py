"""Stop only identity-checked replaced run at a verified recoverable boundary."""
import os,psutil,time,json,hashlib,zipfile,shutil
from pathlib import Path
import torch
root=Path('remote_final_semantic300');proc=psutil.Process(194886)
assert proc.cmdline()[2:] == ['optimization_stage_20261014/run_final_semantic.py','--out','remote_final_semantic300']
identity=dict(pid=proc.pid,created=proc.create_time(),command=proc.cmdline())
for attempt in range(180):
    proc.suspend()
    try:
        state=torch.load(root/'continuation.pt',map_location='cpu',weights_only=False)
        with zipfile.ZipFile(root/'checkpoint.zip') as z:data=json.loads(z.read('data'))
        complete=int(state['completed_updates'])
        valid=data['completed_updates']==complete
    except Exception:valid=False
    if valid:break
    proc.resume();time.sleep(2)
else:raise RuntimeError('No safe checkpoint boundary; preserved live trainer')
archive=root/'pause_speed';archive.mkdir(exist_ok=False)
files={}
for name in ('checkpoint.zip','continuation.pt','metrics.jsonl','manifest.json'):
    shutil.copy2(root/name,archive/name);files[name]=hashlib.sha256((archive/name).read_bytes()).hexdigest()
children=[]
for child in proc.children():
    cmd=child.cmdline()
    if len(cmd)>=2 and Path(cmd[1]).name=='BattleRl.dll':children.append(child)
proc.terminate();proc.resume()
for child in children:
    if child.is_running():child.terminate()
gone,alive=psutil.wait_procs([proc]+children,timeout=15)
if alive:raise RuntimeError('Processes still alive; inspect before any new run')
(root/'speed_pause.json').write_text(json.dumps(dict(identity=identity,completed=complete,reason='Pause for identical-policy sampling speed optimization',files=files,children=[p.pid for p in children],time=time.time()),indent=2))
print(json.dumps(dict(stopped=True,completed=complete,children=len(children))))
