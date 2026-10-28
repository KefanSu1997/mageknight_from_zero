import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,json,collections,re,gc,argparse
from pathlib import Path
sys.path[:0]=['tools/BattleRl','optimization_stage_20261013','optimization_stage_20261014']
import numpy as np
from rollout_archive import load
p=argparse.ArgumentParser();p.add_argument('--root',default='remote_entropy300');p.add_argument('--report',default='optimization_stage_20261014/choice_audit.json');p.add_argument('--end-update',type=int);a=p.parse_args()
root=Path(a.root);out=Path(a.report);wanted={};batches=[]
paths=sorted(root.glob('rollout_*.archive'))[-5:]
if a.end_update is not None:
 paths=[]
 for update in range(a.end_update-5,a.end_update):
  matches=list(root.glob(f'rollout_{update:04d}_*.archive'));assert len(matches)==1,(update,matches)
  paths.append(matches[0])
for path in paths:
 b=load(path);contexts=b.observations['context_id'].ravel().astype(int);actions=b.actions.ravel().astype(int);logs=b.log_probs.ravel();starts=np.flatnonzero(b.episode_starts.ravel());ends=np.r_[starts[1:],len(contexts)]
 for ep,(start,end) in enumerate(zip(starts,ends)):
  for i in range(start,end):wanted[int(contexts[i])]=(int(actions[i]),float(logs[i]),float(b.returns.ravel()[i]),i==start,i==end-1)
 batches.append(dict(path=str(path),steps=len(contexts),episodes=len(starts),mean_log_prob=float(logs.mean()),median_probability=float(np.median(np.exp(logs)))))
 del b;gc.collect()
counts=collections.Counter();available=collections.Counter();samples={};totals=collections.Counter();stats=collections.defaultdict(list);examples=[];support_stats=collections.defaultdict(list)
advance_opportunities=collections.Counter();advance_selected=collections.Counter();advance_examples=[]
with (root/'contexts/contexts.jsonl').open() as f:
 for context,line in enumerate(f,1):
  if context not in wanted:continue
  d=json.loads(line);act,lp,ret,first,last=wanted[context];operations=d['operations'];seq=operations[act];kind=seq[-1]['Kind']
  features={int(k):float(v) for k,v in re.findall(r'state\.(\d+)=([-+\d.eE]+)',d['state'])}
  phase=next((name for i,name in enumerate(['Ranged','Block','AssignDamage','Melee','End'],7) if features.get(i,0)), 'Outside')
  support_stats[(phase,'multiple' if len(operations)>1 else 'forced')].append(float(np.exp(lp)))
  counts[(phase,kind)]+=1;totals[phase]+=1;stats[(phase,kind)].append(np.exp(lp))
  kinds={s[-1]['Kind'] for s in operations}
  # Availability is not proof that a consumer is strategically useful.
  # Report each competing kind separately; these denominators overlap.
  if 'advance' in kinds:
   for competing in sorted(kinds-{'advance'}):
    advance_opportunities[(phase,competing)]+=1
    if kind=='advance':advance_selected[(phase,competing)]+=1
   if kind=='advance' and len(kinds)>1 and len(advance_examples)<20:
    advance_examples.append(dict(context=context,phase=phase,kinds=sorted(kinds),chosen=seq,probability=float(np.exp(lp))))
  for k in kinds:available[(phase,k)]+=1
  key=(phase,kind)
  if key not in samples:samples[key]=dict(context=context,chosen=seq,candidates=len(operations),kinds=sorted(kinds),chosen_probability=float(np.exp(lp)),state=d['state'][:500])
  if np.exp(lp)>.98 and kind in ('advance','end','end_turn') and len(kinds)>1 and len(examples)<10:examples.append(samples[key])
report=dict(root=str(root),batches=batches,phase_totals=dict(totals),choices=[dict(phase=p,kind=k,count=n,available=available[(p,k)],mean_chosen_probability=float(np.mean(stats[(p,k)]))) for (p,k),n in counts.most_common()],samples=list(samples.values()),examples=examples)
report['advance_with_other_kind']=[dict(phase=p,other_kind=k,opportunities=n,advance_selected=advance_selected[(p,k)],fraction=advance_selected[(p,k)]/n) for (p,k),n in sorted(advance_opportunities.items())]
report['advance_examples']=advance_examples
report['support_stats']=[dict(phase=p,support=kind,count=len(values),mean_selected_probability=float(np.mean(values)),median_selected_probability=float(np.median(values)),fraction_selected_probability_above_99=float(np.mean(np.asarray(values)>.99))) for (p,kind),values in support_stats.items()]
out.write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k not in ['samples','examples']}))
