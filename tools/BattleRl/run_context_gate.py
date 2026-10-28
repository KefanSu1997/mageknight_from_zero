import os,sys,json,shutil,zipfile,subprocess,time
from pathlib import Path
import psutil,torch
os.environ['PATH']='/root/autodl-tmp/dotnet:/root/autodl-tmp/mkvenv/bin:'+os.environ['PATH']
root=Path('optimization_stage_20261014');parent=root/'context_parent_snapshot';parent.mkdir(exist_ok=False);paused=[]
try:
 p=psutil.Process(59984);assert 'deploy_entropy_after_eval80.py' in ' '.join(p.cmdline())
 for q in [p]+p.children(recursive=True):paused.append((q.pid,q.create_time()));q.suspend()
 source=Path('remote_entropy300');state=torch.load(source/'continuation.pt',weights_only=False,map_location='cpu')
 with zipfile.ZipFile(source/'checkpoint.zip') as z:assert json.loads(z.read('data'))['completed_updates']==state['completed_updates']
 assert all(s['closed_episode'] for s in state['environments'])
 for name in ['checkpoint.zip','continuation.pt','manifest.json','atomic_supports.jsonl','contexts/contexts.jsonl','metrics.jsonl','evaluation_metrics.jsonl','performance.jsonl','inherited_quarantine.json','audit_quarantine.jsonl']:
  src=source/name
  if src.exists():(parent/name).parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(src,parent/name)
 (parent/'pause_identity.json').write_text(json.dumps(dict(paused=paused,completed=state['completed_updates'])))
 status=dict(state='running',parent_update=state['completed_updates'],started=time.time());(root/'context_gate_status.json').write_text(json.dumps(status))
 result=subprocess.run([sys.executable,'-u',str(root/'resume_context_state.py'),'--parent',str(parent),'--out','remote_context_gate32','--gate'])
 status.update(state='passed' if result.returncode==0 else 'failed',returncode=result.returncode);(root/'context_gate_status.json').write_text(json.dumps(status))
finally:
 for pid,created in reversed(paused):
  try:
   p=psutil.Process(pid)
   if p.create_time()==created:p.resume()
  except psutil.NoSuchProcess:pass
