"""CPU diagnostic on a loaded copy: isolate PPO and entropy root-logit gradients."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,io,json,sqlite3,hashlib,itertools,argparse,re
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import numpy as np
import torch
import train_sb3_atomic_entity_text as base
import hierarchical_option_policy as h
import hierarchical_packed_cache as packed
from rollout_archive import load
from entropy_token_value_policy import bind_reference
p=argparse.ArgumentParser();p.add_argument('--root',required=True);p.add_argument('--out',required=True);a=p.parse_args();root=Path(a.root);torch.set_num_threads(1)
raw=(root/'checkpoint.zip').read_bytes();m=base.RecordedRecurrentPPO.load(io.BytesIO(raw),device='cpu');m.policy.set_training_mode(False)
paths=list(root.glob(f'rollout_{m.completed_updates-1:04d}_*.archive'));assert len(paths)==1
b=load(paths[0]);b.device=torch.device('cpu');m.rollout_buffer=b;bind_reference(m)
db=sqlite3.connect('file:'+str(root/'contexts/packed_vectors/offsets.sqlite')+'?mode=ro',uri=True);stream=(root/'contexts/packed_vectors/vectors.bin').open('rb')
def vectors(path,ctx,node):
 if not ctx:return np.zeros((2,384),np.float32)
 assert Path(path).resolve()==(root/'contexts').resolve()
 row=db.execute('SELECT metadata FROM entries WHERE context=? AND node=?',(int(ctx),int(node))).fetchone();assert row
 meta=json.loads(row[0]);stream.seek(meta['offset']);payload=stream.read(meta['bytes']);assert hashlib.sha256(payload).hexdigest()==meta['sha256']
 return np.frombuffer(payload,dtype='<f4').reshape(meta['shape']).copy()
h.node_vectors=vectors;packed.prefetch=lambda *x:None
captured=[]
def categorical(self,row,node):
 key=(row,node)
 if key not in self.memo:
  v=torch.as_tensor(vectors(self.policy.context_path,int(self.contexts[row]),node)[1:]);op=self.policy.option_head(v);q=self.policy.action_net(self.latent[row:row+1])
  scores=(q*op).sum(-1)/(128**.5)+self.policy.option_bias(op).squeeze(-1)
  self.memo[key]=torch.distributions.Categorical(logits=scores)
  t,doc=h.tree(self.policy.context_path,int(self.contexts[row]));keys=list(t.nodes[node]['children'])
  if node==t.branch(0) and ('完整操作/Kind','advance') in keys:
   features={int(k):float(v) for k,v in re.findall(r'state\.(\d+)=([-+\d.eE]+)',doc['state'])}
   phase=next((name for i,name in enumerate(['Ranged','Block','AssignDamage','Melee','End'],7) if features.get(i,0)), 'unknown')
   captured.append((scores,keys.index(('完整操作/Kind','advance')),int(self.contexts[row]),phase))
 return self.memo[key]
h.HierarchicalDistribution.categorical=categorical
np.random.seed(7112026);records=[]
for batch in itertools.islice(b.get(128),4):
 captured.clear();_,lp,entropy=m.policy.evaluate_actions(batch.observations,batch.actions.long().flatten(),batch.lstm_states,batch.episode_starts)
 assert entropy is not None
 mask=batch.mask>1e-8;adv=batch.advantages
 adv=(adv-adv[mask].mean())/(adv[mask].std()+1e-8)
 ratio=(lp-batch.old_log_prob).exp();pg=-torch.min(adv*ratio,adv*ratio.clamp(.8,1.2))[mask].mean();en=-float(m.ent_coef)*entropy[mask].mean()
 scores=[x[0] for x in captured]
 gp=torch.autograd.grad(pg,scores,retain_graph=True,allow_unused=True);ge=torch.autograd.grad(en,scores,allow_unused=True)
 for (s,i,ctx,phase),g,e in zip(captured,gp,ge):
  if g is None or e is None:continue
  prob=s.detach().softmax(0)
  def delta(grad):return float(-prob[i]*(grad[i]-(prob*grad).sum()))
  records.append(dict(context=ctx,phase=phase,advance_probability=float(prob[i]),ppo_probability_direction=delta(g),entropy_probability_direction=delta(e)))
assert records
summary={}
for phase in sorted({x['phase'] for x in records}):
 rows=[x for x in records if x['phase']==phase]
 summary[phase]={'count':len(rows),'advance_probability_mean':float(np.mean([r['advance_probability'] for r in rows]))}
 for term in ['ppo','entropy']:
  vals=np.array([r[term+'_probability_direction'] for r in rows]);summary[phase][term]={'mean_direction':float(vals.mean()),'positive_fraction':float((vals>0).mean()),'mean_absolute':float(abs(vals).mean())}
report=dict(checkpoint_update=m.completed_updates,checkpoint_sha=hashlib.sha256(raw).hexdigest(),archive=str(paths[0]),entropy_coefficient=float(m.ent_coef),summary=summary,rows=records,scope='Four archived recurrent minibatches at post-update checkpoint, CPU cached vectors only. Local independent-logit gradient directions, not optimizer parameter updates or counterfactual policy improvement. No optimizer or encoder execution.')
Path(a.out).write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k!='rows'}))
