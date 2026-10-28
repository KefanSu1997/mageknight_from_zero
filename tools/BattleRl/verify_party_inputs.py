"""Seven original heroes x legal party levels, ordered damage and an actual ally ready consumer."""
import os
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-parties')
import json
from pathlib import Path
from train_ppo import Rules
from trajectory_audit import audit

metadata=json.loads(Path('tmp/rl_v11_catalog.json').read_text(encoding='utf-8'))
heroes={h['Id']:h for h in metadata['heroes']};env=Rules();rows=[];traces=[];checks=0;ready_fixture=None
try:
    for seed in range(21):
        s=env.reset(seed);b=s['state'];expected_level=(1,3,5)[seed%3]
        # Catalog skills are original IDs, never inferred from translated names.
        skill_ids={skill['SourceId'] for skill in heroes[b['hero']]['skills']}
        tests=[b['level']==expected_level,b['fameLevel']==expected_level,
               len(b['units'])<=1+seed%3,len(b['units'])<=b['commandSlots'],
               len(b['skills'])==expected_level//2,set(b['skills'])<=skill_ids,
               len({u['id'] for u in b['units']})==len(b['units']),
               not any(u['wounds'] or not u['ready'] or u['upkeep'] for u in b['units'])]
        assert all(tests),(seed,tests);checks+=len(tests);rows.append({'seed':seed,'hero':b['hero'],'level':b['level'],'units':b['units'],'skills':b['skills']})
    def step(index):
        global s
        n=env.step(index);traces[-1].append({'before':s['state'],'action':env.actions[index],'after':n['state'],'reward':n['reward']});s=n
    def choose(kind,**fields):
        return next(i for i in s['legal'] if env.actions[i]['Kind']==kind and all(env.actions[i][k]==v for k,v in fields.items()))
    # Search real legal seeds, never inject a post-reset unit or free resource.
    for seed in range(21,321):
        s=env.reset(seed);units=s['state']['units']
        if not any(u['id']=='army_2_002' for u in units):continue
        other={u['id']:i for i,u in enumerate(units) if u['id']!='army_2_002' and u['level']<=2}
        possible=[i for i in s['legal'] if env.actions[i]['Kind']=='unit' and env.actions[i]['Card'] in other]
        if not possible:continue
        traces.append([]);activate=possible[0];ally=env.actions[activate]['Card'];target=other[ally];step(activate)
        support=[i for i in s['legal'] if env.actions[i]['Kind']=='unit' and env.actions[i]['Card']=='army_2_002' and env.actions[i]['Choice']=='1' and env.actions[i]['Targets']==target]
        if not support:traces.pop();continue
        assert not s['state']['units'][target]['ready'];step(support[0]);assert s['state']['units'][target]['ready'];checks+=2
        again=next(i for i in s['legal'] if env.actions[i]['Kind']=='unit' and env.actions[i]['Card']==ally);step(again)
        assert not s['state']['units'][target]['ready'];checks+=1;ready_fixture={'seed':seed,'support':'army_2_002','ally':ally};break
    assert ready_fixture,'No actual herb healer and eligible ally ready consumer'
    # Two different units, exact order visible, confirmed through the formal resolver.
    for seed in range(21):
        s=env.reset(seed)
        if len(s['state']['units'])>=2 and seed%3==2:break
    traces.append([]);step(choose('fight'));step(choose('advance'));step(choose('advance'))
    units=s['state']['units'];step(choose('unitDamage',Choice='10'))
    assert s['state']['unitDamagePlan'][0]['units']==[units[1]['id'],units[0]['id']];checks+=1
    step(choose('unitDamage',Choice='01'))
    assert len(s['state']['unitDamagePlan'])==1 and s['state']['unitDamagePlan'][0]['units']==[units[0]['id'],units[1]['id']];checks+=1
    step(choose('advance'));reports=[audit(t) for t in traces];assert not any(r['errors'] for r in reports),reports
    out=Path('AutomationOutputs/BattleRl/20261003_original_party_inputs.json')
    out.write_text(json.dumps({'scope':env.info['scope'],'fixture_checks':checks,'inventories':rows,'ready_consumer':ready_fixture,'reports':reports,'traces':traces},ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'inventories':len(rows),'fixture_checks':checks,'ready_consumer':ready_fixture,'transition_checks':sum(r['checks'] for r in reports),'errors':0}))
except Exception as error:
    Path('AutomationOutputs/BattleRl/20261003_original_party_inputs_failure.json').write_text(json.dumps({'error':str(error),'rows':rows,'traces':traces},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
