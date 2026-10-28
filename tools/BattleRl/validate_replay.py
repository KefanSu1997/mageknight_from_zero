"""Reload PPO, assert formal legality/no-leak denial, then reproduce exact saved states."""
import argparse,json
from pathlib import Path
import numpy as np
from train_ppo import Rules,Policy,inputs,gradient_check
p=argparse.ArgumentParser();p.add_argument('directory');a=p.parse_args();out=Path(a.directory)
r=json.loads((out/'replay.json').read_text(encoding='utf-8'));env=Rules()
try:
 s=env.reset(r['seed']);assert len(s['features'])==len(env.info['featureNames'])
 (out/'environment.json').write_text(json.dumps(env.info,ensure_ascii=False,indent=2),encoding='utf-8')
 model=Policy(len(s['features']),len(env.actions));model.p=dict(np.load(out/'policy_ppo.npz'))
 illegal=next(i for i in range(len(env.actions)) if i not in s['legal'])
 try:env.step(illegal);raise AssertionError('Illegal action accepted')
 except RuntimeError as e:assert 'formal legal mask' in str(e)
 checks=1
 for t in r['trace']:
  assert s['state']==t['before'];x,m=inputs(s,len(env.actions));_,prob,_=model.forward(x[None],m[None]);action=model.greedy(prob[0])
  assert env.actions[action]==t['action'];s=env.step(action);assert s['state']==t['after'];checks+=3
 assert s['done'] and s['won']
 report={'status':'success','algorithm':'PPO','semanticActions':len(r['trace']),'checks':checks+1,'gradientMaxError':gradient_check(),'stateFields':len(s['state']),'features':len(s['features']),'invalidActionRejectedWithoutMutation':True}
 (out/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report))
finally:env.close()
