"""Verify kind-balanced PPO probabilities on the actual retained planning-loop state."""
import os,json,hashlib,tempfile
from pathlib import Path
import numpy as np
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
from train_ppo import Rules,Policy,inputs,gradient_check
source=Path('AutomationOutputs/BattleRl/20261003_box_same_face_forced_damage_ppo/audit_8192_3.json');saved=json.loads(source.read_text(encoding='utf-8'));env=Rules();checks=0
try:
 s=env.reset(saved['seed']);ids={a['Key']:i for i,a in enumerate(env.actions)}
 for ordinal,row in enumerate(saved['trace'][:18]):
  assert s['state']==row['before'];s=env.step(ids[row['action']['Key']]);assert s['state']==row['after'] and s['reward']==row['reward'];checks+=1
 original=s;d=len(s['features']);n=len(env.actions);x,mask=inputs(s,n)
 net=Policy(d,n);kinds=sorted({a['Kind'] for a in env.actions});groups=np.array([kinds.index(a['Kind']) for a in env.actions],dtype=np.int32)
 net.p['wp'].fill(0);net.p['bp'].fill(0)
 _,flat,_=net.forward(x[None],mask[None]);net.p['action_kind_groups']=groups;_,balanced,_=net.forward(x[None],mask[None])
 legal_kinds=sorted({env.actions[i]['Kind'] for i in s['legal']})
 masses={kind:float(balanced[0,mask&(groups==kinds.index(kind))].sum()) for kind in legal_kinds}
 assert all(abs(v-1/len(legal_kinds))<1e-12 for v in masses.values());checks+=1
 assert np.all(balanced[0,mask]>0) and not np.any(balanced[0,~mask]);checks+=1
 # Actual old saved weights: changing the exploration base measure is not a
 # trained improvement claim. The same formal state/action mask stays intact.
 model=Path('AutomationOutputs/BattleRl/20261003_box_same_face_forced_damage_ppo/policy_checkpoint_8192.npz');net.p=dict(np.load(model));_,old,_=net.forward(x[None],mask[None]);net.p['action_kind_groups']=groups;_,new,value=net.forward(x[None],mask[None])
 change={kind:{'legal_choices':sum(env.actions[i]['Kind']==kind for i in s['legal']),'flat':float(old[0,mask&(groups==kinds.index(kind))].sum()),'balanced':float(new[0,mask&(groups==kinds.index(kind))].sum())} for kind in legal_kinds}
 with tempfile.TemporaryDirectory(dir='tmp') as directory:
  path=Path(directory)/'policy.npz';np.savez(path,**net.p);loaded=Policy(d,n);loaded.p=dict(np.load(path));assert np.array_equal(new,loaded.forward(x[None],mask[None])[1]);checks+=1
 action=int(new[0].argmax());loss,g,kl=loaded.loss_grad(x[None],mask[None],np.array([action]),np.log(new[:,action]),np.array([1.]),value+1)
 loaded.update(g);assert np.array_equal(loaded.p['action_kind_groups'],groups);checks+=1
 observed=env.call(op='state');assert observed['state']==original['state'] and observed['features']==original['features'] and observed['legal']==original['legal'];checks+=1
 result={'source':str(source),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'seed':saved['seed'],'checkpoint':str(model),'checkpoint_sha256':hashlib.sha256(model.read_bytes()).hexdigest(),'checks':checks,'gradient_errors':[gradient_check(),gradient_check(True)],'zero_logit_kind_mass':masses,'old_weights_probability_comparison':change,'actual_game_unchanged':True,'limits':'Only distribution/gradient/serialization/optimizer and exact real prefix; not learned performance, full conditional entity encoder, or Unity replay.'}
 Path('AutomationOutputs/BattleRl/20261003_kind_balanced_planning.json').write_text(json.dumps(result,indent=2),encoding='utf-8');print(json.dumps(result))
finally:env.close()
