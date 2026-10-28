"""Archived on-policy advantage diagnostics; no model updates or counterfactual claims."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,json,argparse,re,collections,hashlib
from pathlib import Path
sys.path[:0]=['tools/BattleRl']
import numpy as np
from rollout_archive import load
p=argparse.ArgumentParser();p.add_argument('--root',required=True);p.add_argument('--end',type=int,required=True);p.add_argument('--out',required=True);a=p.parse_args();root=Path(a.root)
wanted={};sources=[]
for u in range(a.end-5,a.end):
 paths=list(root.glob(f'rollout_{u:04d}_*.archive'));assert len(paths)==1
 b=load(paths[0]);ids=b.observations['context_id'].ravel().astype(int)
 assert len(set(ids))==len(ids)
 for i,ctx in enumerate(ids):
  assert int(ctx) not in wanted
  wanted[int(ctx)]=(int(b.actions.ravel()[i]),float(b.advantages.ravel()[i]),float(b.returns.ravel()[i]),float(b.values.ravel()[i]),float(b.log_probs.ravel()[i]))
 sources.append(str(paths[0]))
remaining_by_support={}
for line in (root/'episode_batches.jsonl').read_text().splitlines():
 batch=json.loads(line)
 if not a.end-4<=batch['update']<=a.end:continue
 for e in batch['episodes']:
  episode=json.loads((root/'episodes'/f"worker_{e['worker']}_episode_{e['episode']}.json").read_text())
  for t in episode['trace']:
   sid=int(t['support_id']);remaining=sum(not x['defeated'] for x in t['before']['enemies'])
   if sid in remaining_by_support:assert remaining_by_support[sid]==remaining
   remaining_by_support[sid]=remaining
groups=collections.defaultdict(list);priors=collections.defaultdict(list);phase_values=collections.defaultdict(list);phase_contexts=collections.defaultdict(list);matched=0
with (root/'contexts/contexts.jsonl').open() as f:
 for ctx,line in enumerate(f,1):
  if ctx not in wanted:continue
  matched+=1;d=json.loads(line);ops=d['operations'];act,adv,ret,val,lp=wanted[ctx]
  features={int(k):float(v) for k,v in re.findall(r'state\.(\d+)=([-+\d.eE]+)',d['state'])}
  phase=next((n for i,n in enumerate(['Ranged','Block','AssignDamage','Melee','End'],7) if features.get(i,0)), 'Outside')
  kinds={s[-1]['Kind'] for s in ops};kind=ops[act][-1]['Kind']
  phase_values[phase].append((ret,val))
  phase_contexts[phase].append(ctx)
  remaining=remaining_by_support[int(d['support_id'])]
  subgroup=phase+('/enemies_remaining' if remaining else '/already_cleared')
  phase_values[subgroup].append((ret,val));phase_contexts[subgroup].append(ctx)
  if len(kinds)>1 and 'advance' in kinds:
   groups[(phase,kind)].append((adv,ret,val,float(np.exp(lp))))
   advance_leaves=sum(s[-1]['Kind']=='advance' for s in ops)
   priors[phase].append((1/len(kinds),advance_leaves/len(ops)))
assert matched==len(wanted)
rows=[]
for (phase,kind),values in sorted(groups.items()):
 x=np.array(values);rows.append(dict(phase=phase,kind=kind,count=len(x),mean_advantage=float(x[:,0].mean()),positive_advantage_fraction=float((x[:,0]>0).mean()),mean_return=float(x[:,1].mean()),mean_value=float(x[:,2].mean()),mean_selected_probability=float(x[:,3].mean())))
r=dict(scope='Raw saved advantages before per-minibatch normalization. Selected actions on different states; not causal, not counterfactual Q estimates. Uniform priors are structural references, not trained probabilities.',sources=sources,matched=matched,groups=rows,uniform_advance_priors={k:dict(states=len(v),hierarchical_mean=float(np.mean(v,axis=0)[0]),flat_leaf_mean=float(np.mean(v,axis=0)[1])) for k,v in priors.items()})
r['phase_value_fit']={}
for phase,values in phase_values.items():
 x=np.array(values);target,pred=x[:,0],x[:,1];variance=float(np.var(target))
 r['phase_value_fit'][phase]=dict(states=len(x),target_variance=variance,ev=None if variance<=1e-12 else float(1-np.var(target-pred)/variance),ev_variance_floor=1e-12,mse=float(np.mean((pred-target)**2)),bias=float(np.mean(pred-target)))
 error=(pred-target)**2;order=np.argsort(error)[::-1];tail=max(1,int(np.ceil(len(x)*.05)))
 r['phase_value_fit'][phase].update(prediction_quantiles=np.quantile(pred,[0,.05,.5,.95,1]).tolist(),target_quantiles=np.quantile(target,[0,.05,.5,.95,1]).tolist(),largest_5pct_error_share=None if error.sum()==0 else float(error[order[:tail]].sum()/error.sum()),largest_errors=[dict(context=phase_contexts[phase][i],target=float(target[i]),prediction=float(pred[i]),squared_error=float(error[i])) for i in order[:10]])
Path(a.out).write_text(json.dumps(r,indent=2));print(json.dumps(r))
