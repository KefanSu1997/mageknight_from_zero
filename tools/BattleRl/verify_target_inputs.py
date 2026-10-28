"""Original ice-bat Defend choices, without adding cards or free resources."""
import os
os.environ['BATTLE_RL_PROTOCOL']='--serve-journey'
import json
from pathlib import Path
from train_ppo import Rules
from trajectory_audit import audit

env=Rules();trace=[]
try:
    s=env.reset(392)  # Original monster_057 and two other physical original tokens.
    assert len(s['state']['enemies'])==3
    assert s['state']['enemies'][0]['id']=='monster_057'
    assert len(s['state']['enemies'][0]['attacks'])==2
    def step(kind,**fields):
        global s
        index=next(i for i in s['legal'] if env.actions[i]['Kind']==kind and all(env.actions[i][k]==v for k,v in fields.items()))
        n=env.step(index);trace.append({'before':s['state'],'action':env.actions[index],'after':n['state'],'reward':n['reward']});s=n
    step('fight')
    step('defend',Targets=7,Parts=0,Option=2)
    assert s['state']['defenderSelected'][0]['target']==s['state']['enemies'][2]['instance']
    assert not s['state']['defenderApplied']
    step('clearDefend')
    assert not s['state']['defenderSelected']
    step('defend',Targets=3,Parts=0,Option=1)
    assert s['state']['defenderSelected'][0]['target']==s['state']['enemies'][1]['instance']
    assert not s['state']['defenderApplied']
    result=audit(trace);assert not result['errors'],result
    out=Path('AutomationOutputs/BattleRl/20261003_defend_target_inputs.json')
    out.write_text(json.dumps({'scope':env.info['scope'],'seed':392,'report':result,'trace':trace},ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'operations':len(trace),'checks':result['checks'],'errors':result['errors'],'evidence':str(out)}))
finally:
    env.close()
