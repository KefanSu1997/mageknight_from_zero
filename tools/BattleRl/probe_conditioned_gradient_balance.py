"""Offline actual recurrent minibatch gradients; no optimizer update."""
import os
os.environ['OPENBLAS_NUM_THREADS']='1'
import sys,json,io,itertools
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
# This module performs the read-only CPU migration gate and installs a strict
# cached-vector reader that refuses encoder fallback. It does not train.
import gate_conditioned_policy_cpu as gate
import torch,numpy as np
from entropy_token_value_policy import bind_reference
from rollout_archive import load
torch.set_num_threads(1)
original=gate.base.RecordedRecurrentPPO.load(io.BytesIO(gate.raw),device='cpu')
archive=sorted(gate.root.glob('rollout_*.archive'))[-1]
b=load(archive);b.device=torch.device('cpu')
original.rollout_buffer=b;gate.m.rollout_buffer=b
bind_reference(original);bind_reference(gate.m)
np.random.seed(7112026);batches=list(itertools.islice(b.get(128),4))
def group(name):
 if name.startswith('features_extractor'):return 'shared_state'
 if 'lstm_actor' in name or 'policy_net' in name:return 'actor_recurrent'
 if 'lstm_critic' in name or 'value_net' in name:return 'critic_recurrent'
 if 'token_value' in name or 'value_attention' in name:return 'text_value'
 return 'action_readout'
def measure(label,model,batch,index):
 valid=batch.mask>1e-8
 model.policy.set_training_mode(False)
 values,logp,entropy=model.policy.evaluate_actions(batch.observations,batch.actions.long().flatten(),batch.lstm_states,batch.episode_starts)
 values=values.flatten();adv=batch.advantages
 adv=(adv-adv[valid].mean())/(adv[valid].std()+1e-8)
 ratio=torch.exp(logp-batch.old_log_prob)
 policy=-torch.min(adv*ratio,adv*ratio.clamp(.8,1.2))[valid].mean()
 value=.5*(values[valid]-batch.returns[valid]).square().mean()
 ent=-.01*entropy[valid].mean()
 named=list(model.policy.named_parameters());parameters=[p for _,p in named]
 norms={};all_grads={}
 for loss_name,loss in [('policy',policy),('value_weighted',value),('entropy_weighted',ent),('total',policy+value+ent)]:
  grads=torch.autograd.grad(loss,parameters,retain_graph=True,allow_unused=True)
  sums={}
  for (name,p),g in zip(named,grads):
   if g is None:continue
   assert torch.isfinite(g).all()
   key=group(name);sums[key]=sums.get(key,0.)+float(g.square().sum())
  norms[loss_name]={k:v**.5 for k,v in sums.items()};norms[loss_name]['all']=sum(sums.values())**.5
 return dict(batch=index,policy=label,valid_samples=int(valid.sum()),padded_samples=len(valid),losses=dict(policy=float(policy.detach()),value_weighted=float(value.detach()),entropy_weighted=float(ent.detach())),gradient_norms=norms,global_clip_multiplier=min(1.,.5/(norms['total']['all']+1e-6)))
results=[measure(label,model,batch,index) for index,batch in enumerate(batches) for label,model in [('current',original),('conditioned',gate.m)]]
report=dict(parent_update=original.completed_updates,archive=str(archive),results=results,scope='Four saved real recurrent minibatches with stored LSTM starts, no optimizer step. Candidate and current share actions, targets and initial outputs. This is one rollout and cannot establish long-run gradient balance or policy improvement.')
Path('optimization_stage_20261014/conditioned_gradient_balance_four.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
