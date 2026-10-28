"""Original Flame Gunners' paid fire ranged attack, free block and real damage plan."""
import os
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-units')
import json
from pathlib import Path
from train_ppo import Rules
from trajectory_audit import audit

env=Rules();traces=[];checks=0;seed=None
try:
    for candidate in range(2820,3120):
        s=env.reset(candidate)
        if s['state']['units'] and s['state']['units'][0]['id']=='army_003' and s['state']['crystals'].get('Red',0)>0:
            seed=candidate;break
    assert seed is not None,'No actual original Flame Gunners inventory with printed red fee'
    def step(kind,**selection):
        global s
        choices=[i for i in s['legal'] if env.actions[i]['Kind']==kind and all(env.actions[i][key]==value for key,value in selection.items())]
        assert choices,(kind,selection)
        action=choices[0];n=env.step(action)
        traces[-1].append({'before':s['state'],'action':env.actions[action],'after':n['state'],'reward':n['reward']});s=n
    def stock():return sum(s['state']['tokens'].get(c,0)+s['state']['crystals'].get(c,0) for c in ('Red','Gold'))
    traces.append([]);step('fight');before=stock();step('unit',Card='army_003',Choice='1')
    assert before-stock()==1;checks+=1
    parts=[p for p in s['state']['attacks'] if p.get('unit')=='army_003']
    assert len(parts)==1 and parts[0]['value']==6 and parts[0]['element']=='Fire' and parts[0]['type']=='Ranged';checks+=1
    assert not s['state']['units'][0]['ready'];checks+=1
    assert not any(env.actions[i]['Kind']=='unit' and env.actions[i]['Card']=='army_003' for i in s['legal']);checks+=1
    step('advance');step('advance');before_units=s['state']['units'];step('unitDamage',Option=0)
    assert s['state']['units']==before_units and s['state']['unitDamagePlan'];checks+=1
    step('advance');assert s['state']['units'][0]['wounds']>0;checks+=1
    while s['state']['phase']!='End':step('advance')
    wounded=s['state']['units'][0]['wounds'];step('nextTurn')
    assert s['state']['units'][0]['wounds']==wounded and not s['state']['units'][0]['ready'];checks+=1
    s=env.reset(seed);traces.append([]);step('fight');step('advance');before_block=s['state']['block'];before=stock()
    step('unit',Card='army_003',Choice='0',Option=1)
    assert s['state']['block']-before_block==5 and before==stock();checks+=1
    reports=[audit(t) for t in traces];assert not any(r['errors'] for r in reports),reports
    path=Path('AutomationOutputs/BattleRl/20261003_original_unit_inputs.json')
    path.write_text(json.dumps({'scope':env.info['scope'],'seed':seed,'original_source':'resources/text_json/army.json army_003','fixture_checks':checks,'reports':reports,'traces':traces},ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'seed':seed,'fixture_checks':checks,'transition_checks':sum(r['checks'] for r in reports),'errors':0}))
except Exception as error:
    Path('AutomationOutputs/BattleRl/20261003_original_unit_inputs_failure.json').write_text(json.dumps({'seed':seed,'error':str(error),'traces':traces},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
