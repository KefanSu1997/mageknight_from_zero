"""Actual v37 policy observation and native cumbersome consumer; no teacher policy."""
import json
from pathlib import Path
from train_ppo import Rules,validate_ability_schema
from trajectory_audit import audit

env=Rules()
result={'scope':'actual protocol and observation; no trained-policy or Unity acceptance'}
trace=[]

def step(state,index):
    after=env.step(index)
    trace.append({'before':state['state'],'action':env.actions[index],'after':after['state']})
    return after

def kind(state,name):
    return step(state,next(i for i in state['legal'] if env.actions[i]['Kind']==name))

try:
    assert env.info['enemySlots']==6
    for name in ('block','cumbersome','unitDamage','killCrystal'):
        assert {a['Targets'] for a in env.actions if a['Kind']==name}==set(range(6)),name
    assert any(a['Kind']=='attack' and a['Targets']==32 for a in env.actions)
    assert any(a['Kind']=='defend' and a['Parts']==5 and a['Option']==5 for a in env.actions)
    result['all_six_target_catalog_slots']=True
    # Same ability list does not make reordered target blocks compatible.
    old=dict(env.info);old.pop('featureSchema')
    try:validate_ability_schema(old,env.info)
    except ValueError:result['old_feature_schema_rejected']=True
    else:raise AssertionError('Old reordered feature blocks accepted')
    state=env.reset(155);state=kind(state,'fight');state=kind(state,'advance')
    assert len(state['state']['enemies'])==4
    block=state['features'][-6*74:]
    assert len(block)==444
    for binding in state['state']['summoning']['bindings']:
        values=block[binding['index']*74:(binding['index']+1)*74]
        assert values[0]==1 and values[1+binding['originalIndex']]==1
        assert sum(values[1:4])==1
        assert values[4+int(binding['summonerId'].split('_')[-1])-1]==1
        assert sum(values[4:73])==1
    result['four_defender_parent_observation']=True
    # Native original 053 has printed cumbersome; use an actual dealt March.
    trace.clear()
    for seed in range(364,371):
        state=env.reset(seed);trace.clear()
        state=kind(state,'fight');state=kind(state,'advance')
        move=next((i for i in state['legal'] if env.actions[i]['Kind']=='play' and env.actions[i]['Card']=='basic_card_000' and env.actions[i]['Choice']=='base'),None)
        if move is not None:break
    else:raise AssertionError('No original March in actual seeded hands')
    state=step(state,move)
    assert state['state']['movement']==2
    before=state['state']['enemies'][0]['attacks'][0]['effectiveAttack']
    for point in range(2):
        selected=next(i for i in state['legal'] if env.actions[i]['Kind']=='cumbersome' and env.actions[i]['Targets']==0 and env.actions[i]['Option']==0)
        state=step(state,selected)
    assert state['state']['movement']==0
    assert state['state']['enemies'][0]['attacks'][0]['effectiveAttack']==before-2
    assert not any(env.actions[i]['Kind']=='cumbersome' for i in state['legal'])
    checked=audit(trace);assert not checked['errors'],checked
    result.update(status='passed',seed=seed,native_cumbersome_audit=checked,
                  features=len(state['features']),actions=len(env.actions),trace=trace)
    print(json.dumps({k:v for k,v in result.items() if k!='trace'},ensure_ascii=False))
except Exception as error:
    result.update(status='failed',error=str(error),trace=trace)
    raise
finally:
    Path('AutomationOutputs/BattleRl/20261004_v37_six_target_observation.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    env.close()
