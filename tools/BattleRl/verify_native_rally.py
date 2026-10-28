"""Exercise native Rally on actual owned units after formal battle and turn transitions."""
import json,time
from pathlib import Path
from copy import deepcopy
import numpy as np
from train_ppo import Rules,Policy
from trajectory_audit import audit

env=Rules();skill='hero_skill_pool_002:02';traces=[];fixtures=[]
model=Path('AutomationOutputs/BattleRl/20261004_native_puppet_ppo')
weights=dict(np.load(model/'policy_ppo.npz'));info=json.loads((model/'environment.json').read_text(encoding='utf-8'))
known={a['Key']:i for i,a in enumerate(info['catalog'])};mapping={a['Key']:i for i,a in enumerate(env.actions)}
net=Policy(weights['w1'].shape[0],len(info['catalog']));net.p=weights

def teacher():
    mask=np.zeros(len(info['catalog']),dtype=bool)
    for i in state['legal']:
        if env.actions[i]['Key'] in known:mask[known[env.actions[i]['Key']]]=True
    assert mask.any()
    _,prob,_=net.forward(np.array(state['features'][:weights['w1'].shape[0]])[None],mask[None])
    return mapping[info['catalog'][net.greedy(prob[0])]['Key']]

def step(i):
    global state
    n=env.step(i);traces[-1].append({'before':state['state'],'action':env.actions[i],'after':n['state'],'reward':n['reward']});state=n

try:
    seeds=[]
    for seed in range(3,1400,7):
        s=env.call(op='inspectInventory',seed=seed)['state']
        if skill in s['skills'] and len(s['units'])>=2:seeds.append(seed)
        if len(seeds)>=18:break
    assert seeds,'No real legal Norowas party with owned Rally'
    for mode in (0,1):
        for seed in seeds:
            state=env.reset(seed);traces.append([]);event=None;consumer=False
            for _ in range(256):
                if state['done']:break
                options=[i for i in state['legal'] if env.actions[i]['Kind']=='skill' and env.actions[i]['Card']==skill and env.actions[i]['Option']==mode and env.actions[i]['Targets']>0]
                if event is None and options:
                    chosen=options[0];before=deepcopy(state['state']);unit=env.actions[chosen]['Targets'];step(chosen);after=state['state']
                    assert before['phase']==after['phase']=='Outside'
                    assert skill not in before['usedSkillsRound'] and skill in after['usedSkillsRound']
                    assert before['units'][unit]['id']==after['units'][unit]['id']
                    assert all(before['units'][j]==after['units'][j] for j in range(len(before['units'])) if j!=unit)
                    if mode==0:assert not before['units'][unit]['ready'] and after['units'][unit]['ready']
                    else:assert after['units'][unit]['wounds']==before['units'][unit]['wounds']-1
                    assert all(before[k]==after[k] for k in ('zones','tokens','crystals','source','held'))
                    assert not any(env.actions[i]['Kind']=='skill' and env.actions[i]['Card']==skill for i in state['legal'])
                    assert env.call(op='state')['state']==after,'Preview changed real units'
                    event={'target_index':unit,'before':before['units'][unit],'after':after['units'][unit],'round':after['rounds'],'seed':seed,'mode':mode}
                    continue
                if event:
                    # Reuse the same restored/healed original unit through its formal ability entry.
                    actual=[i for i in state['legal'] if env.actions[i]['Kind']=='unit' and env.actions[i]['Card']==event['after']['id']]
                    if actual:step(actual[0]);consumer=True;break
                step(teacher())
            if event and consumer:
                report=audit(traces[-1]);assert not report['errors'],report
                fixtures.append({**event,'actual_later_unit_consumer':consumer,'audit':report});break
            traces.pop()
        else:raise AssertionError(('No real nonfirst-unit Rally and later ability fixture',mode,seeds))
    for fixture,trace in zip(fixtures,traces):
        bad=deepcopy(trace);event=next(t for t in bad if t['action']['Kind']=='skill' and t['action']['Card']==skill)
        event['action']['Targets']=0
        rejected=audit(bad)['errors']
        assert any(e['rule']=='native Rally affects only selected unit' for e in rejected),'Wrong-target negative did not execute new oracle'
        fixture['wrong_target_negative']=rejected
    result={'scope':env.info['scope'],'fixtures':fixtures,'traces':traces,'limits':'two native nonfirst owned-unit branches; no PPO use claim, no full round-reset or Unity acceptance'}
    Path('AutomationOutputs/BattleRl/20261004_native_rally.json').write_text(json.dumps(result,ensure_ascii=False),encoding='utf-8')
    print(json.dumps(fixtures,ensure_ascii=False))
except Exception as e:
    Path(f'AutomationOutputs/BattleRl/20261004_native_rally_failure_{time.time_ns()}.json').write_text(json.dumps({'error':str(e),'fixtures':fixtures,'traces':traces},ensure_ascii=False),encoding='utf-8');raise
finally:env.close()
