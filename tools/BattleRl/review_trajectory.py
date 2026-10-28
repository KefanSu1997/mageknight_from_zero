"""Replay one saved PPO policy against current rules, retaining the first oracle failure."""
import os
os.environ['BATTLE_RL_PROTOCOL']='--serve-journey'
import argparse,json
from pathlib import Path
import numpy as np
from train_ppo import Rules,Policy,inputs
from trajectory_audit import audit

def main():
    p=argparse.ArgumentParser();p.add_argument('--model',required=True);p.add_argument('--seed',required=True,type=int);p.add_argument('--out',required=True);p.add_argument('--replay');p.add_argument('--expect-denial-at',type=int);a=p.parse_args();env=Rules();trace=[];denial=None
    try:
        s=env.reset(a.seed);net=Policy(len(s['features']),len(env.actions));net.p=dict(np.load(a.model))
        old=json.loads(Path(a.replay).read_text(encoding='utf-8'))['trace'] if a.replay else []
        while not s['done']:
            x,m=inputs(s,len(env.actions));_,prob,_=net.forward(x[None],m[None]);action=net.greedy(prob[0])
            if len(trace)<len(old):action=next(i for i,m in enumerate(env.actions) if m['Key']==old[len(trace)]['action']['Key'])
            if a.expect_denial_at==len(trace) and old:
                assert action not in s['legal'],'Historical failing action is still legal'
                try:env.step(action)
                except RuntimeError as error:denial={'step':len(trace),'action':env.actions[action],'error':str(error)}
                else:raise AssertionError('Historical failing action was not rejected')
                observed=env.call(op='state')
                assert all(observed[k]==s[k] for k in ('state','features','legal','steps')),'Rejected action mutated actual state'
                denial['unchanged']=True;old=[]
                action=net.greedy(prob[0])
            try:n=env.step(action)
            except RuntimeError as error:
                path=Path(a.out);path.parent.mkdir(parents=True,exist_ok=True)
                path.write_text(json.dumps({'scope':env.info['scope'],'seed':a.seed,'replay_blocked_at':len(trace),'action':env.actions[action],'error':str(error),'current':s['state'],'historical':old[len(trace)]['before'] if old else None,'trace':trace},ensure_ascii=False,indent=2),encoding='utf-8')
                raise
            trace.append({'before':s['state'],'action':env.actions[action],'after':n['state'],'reward':n['reward']});s=n
            report=audit(trace)
            if report['errors']:break
        report=audit(trace);path=Path(a.out);path.parent.mkdir(parents=True,exist_ok=True)
        if a.expect_denial_at is not None:assert denial is not None,'Expected historical denial was not reached'
        path.write_text(json.dumps({'scope':env.info['scope'],'seed':a.seed,'model':a.model,'report':report,'historical_denial':denial,'trace':trace},ensure_ascii=False,indent=2),encoding='utf-8')
        print(json.dumps({'seed':a.seed,'steps':len(trace),'checks':report['checks'],'errors':report['errors'],'gaps':report['adapter_gaps']},ensure_ascii=False),flush=True)
        if report['errors']:raise SystemExit(2)
    finally:env.close()

if __name__=='__main__':main()
