"""Reconcile selected full real episodes with saved PPO sequences and MC targets."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,json,argparse,hashlib
from pathlib import Path
sys.path.insert(0,'tools/BattleRl')
import numpy as np
from rollout_archive import load
p=argparse.ArgumentParser();p.add_argument('--root',required=True);p.add_argument('--outliers',required=True);p.add_argument('--out',required=True);a=p.parse_args();root=Path(a.root)
paths=sorted({r['episode'] for r in json.loads(Path(a.outliers).read_text())['rows']});episodes={p:json.loads(Path(p).read_text()) for p in paths}
support_to_trace={int(t['support_id']):(path,i) for path,d in episodes.items() for i,t in enumerate(d['trace'])};context_to_trace={}
with (root/'contexts/contexts.jsonl').open() as f:
 for ctx,line in enumerate(f,1):
  d=json.loads(line);sid=int(d['support_id'])
  if sid in support_to_trace:context_to_trace[ctx]=support_to_trace[sid]
assert len(context_to_trace)==len(support_to_trace)
found={};boundary_checks=[]
for u in range(45,50):
 archives=list(root.glob(f'rollout_{u:04d}_*.archive'));assert len(archives)==1
 b=load(archives[0]);ids=b.observations['context_id'].ravel().astype(int);starts=b.episode_starts.ravel();rets=b.returns.ravel();rewards=b.rewards.ravel()
 for i,ctx in enumerate(ids):
  if ctx not in context_to_trace:continue
  path,step=context_to_trace[ctx];t=episodes[path]['trace'][step]
  assert bool(starts[i])==(step==0)
  assert abs(float(rewards[i])-float(t['training_reward']))<1e-5
  end=i+len(episodes[path]['trace'])-step
  assert end<=len(ids)
  assert not np.any(starts[i+1:end])
  assert end==len(ids) or starts[end]
  mc=float(sum(float(x['training_reward']) for x in episodes[path]['trace'][step:]))
  assert abs(mc-float(rets[i]))<1e-5,(ctx,mc,float(rets[i]))
  row=dict(context=int(ctx),step=step,phase=t['before']['phase'],action=t['action']['Kind'],prediction=float(b.values.ravel()[i]),target=float(rets[i]),reward=float(rewards[i]),hand=len(t['before']['hand']),remaining_enemies=sum(not e['defeated'] for e in t['before']['enemies']))
  if step==0:
   row['stored_vf_hidden_max']=float(abs(b.hidden_states_vf[i]).max());row['stored_vf_cell_max']=float(abs(b.cell_states_vf[i]).max())
   row['episode_start_mask']=float(starts[i])
  found[(path,step)]=row
assert len(found)==len(support_to_trace)
report=dict(scope='Four selected real complete episodes; exact archive IDs, start masks, trace rewards and undiscounted suffix returns checked. Saved start hidden states are reported, not proof of full recurrent replay.',passed=True,episodes=[])
for path,d in episodes.items():
 rows=[found[(path,i)] for i in range(len(d['trace']))]
 report['episodes'].append(dict(path=path,sha256=hashlib.sha256(Path(path).read_bytes()).hexdigest(),won=d['won'],rows=rows))
Path(a.out).write_text(json.dumps(report,indent=2));print(json.dumps(dict(passed=True,episodes=len(episodes),steps=len(found),starts=[e['rows'][0] for e in report['episodes']])))
