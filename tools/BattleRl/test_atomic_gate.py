import os,json,sys,time,copy
from pathlib import Path
import numpy as np
import torch
from train_sb3_atomic import configure_rules
from train_ppo import Rules
from action_argument_features import catalog_argument_features
from printed_argument_features import extend_arguments
from atomic_battle_env import BattleEnv
from atomic_battle_policy import AtomicBattlePolicy
from sb3_battle_vec import ThreadedRulesVecEnv
from sb3_contrib import RecurrentPPO
from trajectory_audit import audit

out=Path(sys.argv[1]);out.mkdir(parents=True,exist_ok=False)
os.environ['BATTLE_RL_ATOMIC_REGISTRY']=str(out/'supports.jsonl')
configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_build/BattleRl.dll')
torch.set_num_threads(1)
r=Rules();history=catalog_argument_features(r.actions);desc,_=extend_arguments(r.actions,history);r.close()
np.save(out/'descriptors.npy',desc)
from task_feasibility import file_hash
rows=json.load(open('H:/MageKnight_RL_Runs/atomic_dataset/train.json'))['tasks']
heroes={}
for row in rows:heroes.setdefault(row['task']['definition']['HeroId'],row)
records=[]
for worker,row in enumerate(heroes.values()):
 e=BattleEnv([row],out/'episodes',worker,history,reward_config={'version':'single_battle_v2','scale':.01})
 try:
  e.reset()
  for step in range(12):
   candidates=[(i,p) for i,p in enumerate(e.choices) if p[-1]['Kind'] in ('advance','nextTurn')]
   assert candidates, 'Missing formal phase/cleanup operation'
   a=max(candidates,key=lambda v:len(v[1]))[0]
   before=copy.deepcopy(e.current)
   obs,reward,done,_,info=e.step(a)
   assert not any(p[-1]['Kind'] in ('unitDamage','payment','sharedArm','defend') for p in e.choices)
   if done:
    assert 'audit_failure' not in info,info
    assert e.current['state']['completedBattles']==1
    assert not any(t['action']['Kind'] in ('rest','fight') for t in e.micro_trace)
    bad=copy.deepcopy(e.micro_trace);bad[-1]['after']['completedBattles']=2
    assert audit(bad)['errors'],'Boundary negative control missed duplicate completion'
    records.append(dict(hero=row['task']['definition']['HeroId'],steps=step+1,micro=len(e.micro_trace)))
    break
  else:raise AssertionError('Did not complete single battle')
 finally:e.close()
 print(records[-1],flush=True)
v=ThreadedRulesVecEnv([lambda i=i:BattleEnv(rows[i:i+1],out/'cuda',i,history,reward_config={'version':'single_battle_v2','scale':.01}) for i in range(2)])
try:
 m=RecurrentPPO(AtomicBattlePolicy,v,n_steps=4,batch_size=4,n_epochs=1,device='cuda',seed=101,gamma=1.,gae_lambda=1.,policy_kwargs=dict(descriptor_file=str(out/'descriptors.npy'),descriptor_sha256=file_hash(out/'descriptors.npy'),registry_file=str(out/'supports.jsonl')))
 before={k:p.detach().clone() for k,p in m.policy.named_parameters()}
 m.learn(total_timesteps=8)
 assert all(torch.isfinite(p).all() for p in m.policy.parameters())
 changed=[k for k,p in m.policy.named_parameters() if not torch.equal(p,before[k])]
 assert any('lstm' in k for k in changed) and any('argument_encoder' in k for k in changed)
 m.save(out/'smoke_checkpoint')
 result=dict(heroes=records,cuda=str(m.device),gradient_changed=changed,scope='interface and autograd gate; not production training or performance evidence')
 (out/'gate.json').write_text(json.dumps(result,indent=2))
 print('PASS',flush=True)
finally:v.close()
