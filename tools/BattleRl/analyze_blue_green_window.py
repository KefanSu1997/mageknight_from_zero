import json,hashlib,argparse,collections
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--roots',nargs='+');p.add_argument('--out');a=p.parse_args()
metrics={};batches={}
for r in map(Path,a.roots):
 for x in (r/'metrics.jsonl').read_text().splitlines():
  d=json.loads(x);metrics[d['update']]=d
 for x in (r/'episode_batches.jsonl').read_text().splitlines():
  d=json.loads(x);batches[d['update']]=(r,d)
rows=[];files=[]
for u in range(30,90):
 r,b=batches[u]
 for e in b['episodes']:
  f=r/'episodes'/f"worker_{e['worker']}_episode_{e['episode']}.json";raw=f.read_bytes();d=json.loads(raw);tr=d['trace'];s=tr[0]['before'];end=tr[-1]['after'];perf=d['performance']
  actions=collections.Counter(t['action']['Kind'] for t in tr)
  mode=collections.Counter(t['action'].get('Choice','') for t in tr if t['action']['Kind']=='play')
  rows.append(dict(update=u,task=e['task_id'],won=int(d['won']),hero=s['hero'],enemies=len(s['enemies']),armor=sum(x.get('effectiveArmor',0) for x in s['enemies']),units=len(s['units']),initial_hand=len(s['hand']),steps=len(tr),kills=perf['kills'],wounds=perf['new_wound_cards'],unit_wounds=perf['new_unit_wounds'],reward=perf['training_return'],timeout=perf.get('timeout'),stop=perf['stop_reason'],actions=dict(actions),modes=dict(mode),remaining_hand=len(end['hand']),ready_units=sum(x['ready'] and not x['destroyed'] for x in end['units']),errors=len(d['report']['errors'])))
  files.append(dict(path=str(f),sha256=hashlib.sha256(raw).hexdigest()))
 print(u,flush=True)
Path(a.out).write_text(json.dumps(dict(rows=rows,files=files,metrics=[metrics[u] for u in range(30,90)])))
