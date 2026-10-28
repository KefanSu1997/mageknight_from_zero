"""Original bow per-contribution modes, split shield, and actual kill crystal choices."""
import os,json
from pathlib import Path
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
from train_ppo import Rules
from trajectory_audit import audit

env=Rules();traces=[];checks=0;seeds={}
def choose(kind,**fields):
    return next(i for i in s['legal'] if env.actions[i]['Kind']==kind and all(env.actions[i][k]==v for k,v in fields.items()))
def step(index):
    global s
    n=env.step(index);traces[-1].append({'before':s['state'],'action':env.actions[index],'after':n['state'],'reward':n['reward']});s=n
try:
    free_ranged={'army_007','army_014','army_2_010','army_2_012'}
    for seed in range(4000):
        inv=env.call(op='inspectInventory',seed=seed)['state']
        for id in ('items_019','items_020'):
            if id in inv['hand'] and (id!='items_019' or not any('Paralyze' in e['abilities'] for e in inv['enemies'])):seeds.setdefault(id,seed)
        if 'items_016' in inv['hand'] and any(u['id'] in free_ranged for u in inv['units']):seeds.setdefault('items_016',seed)
        if len(seeds)==3:break
    assert len(seeds)==3,seeds
    s=env.reset(seeds['items_016']);traces.append([]);step(choose('fight'));step(choose('artifact',Card='items_016',Choice='burn'))
    actual=next(u for u in s['state']['units'] if u['id'] in free_ranged)
    step(choose('unit',Card=actual['id'],Choice='1' if actual['id']=='army_2_012' else '0'))
    part=s['state']['attacks'][0];assert part['type']=='Ranged';checks+=1
    for choice in (2,1,0):
        before=s['features'];step(choose('bowChoice',Targets=0,Option=choice))
        assert s['state']['attacks'][0]['starBowChoice']==choice and s['features']!=before;checks+=1
        assert s['state']['attacks'][0]['value']==part['value'] and s['state']['attacks'][0]['element']==part['element'];checks+=1
        expected=part['effectiveValue']//2 if choice==2 else part['effectiveValue']
        assert s['state']['attacks'][0]['effectiveValue']==expected;checks+=1
        assert s['state']['attacks'][0]['effectiveSiege']==(choice==2);checks+=1
    # Ordinary shield offers two independent blocks of 4, never one pool of 8.
    for mode in ('normal','burn'):
        s=env.reset(seeds['items_020']);traces.append([]);step(choose('fight'));step(choose('advance'));step(choose('artifact',Card='items_020',Choice=mode,Option=1))
        blocks=s['state']['blocks'];assert len(blocks)==(2 if mode=='normal' else 3);checks+=1
        assert all(b['value']==4 and b['element']==('Physical' if mode=='normal' else 'ColdFire') for b in blocks);checks+=1
        assert len({b['distinctGroup'] for b in blocks})==1 and blocks[0]['distinctGroup']>0;checks+=1
    s=env.reset(seeds['items_019']);traces.append([]);step(choose('fight'))
    while s['state']['phase']!='Melee':step(choose('advance'))
    step(choose('artifact',Card='items_019',Choice='normal'))
    before=s['features'];step(choose('killCrystal',Targets=0,Option=3))
    assert s['state']['killCrystalChoices']['0']=='White' and s['features']!=before;checks+=1
    enemy=s['state']['enemies'][0]
    allowed={3}|({0} if 'FireResist' in enemy['abilities'] else set())|({1} if 'IceResist' in enemy['abilities'] else set())|({2} if 'PhysicalResist' in enemy['abilities'] else set())
    assert all(env.actions[i]['Option'] in allowed for i in s['legal'] if env.actions[i]['Kind']=='killCrystal' and env.actions[i]['Targets']==0);checks+=1
    reports=[audit(t) for t in traces];assert not any(r['errors'] for r in reports),reports
    result={'scope':env.info['scope'],'seeds':seeds,'fixture_checks':checks,'transition_checks':sum(r['checks'] for r in reports),'reports':reports,'traces':traces,'limitation':'choice and printed allocation checks; subsequent combat consumers still require training and scene evidence'}
    Path('AutomationOutputs/BattleRl/20261003_original_artifact_choices.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:result[k] for k in ('scope','seeds','fixture_checks','transition_checks')},ensure_ascii=False))
except Exception as error:
    Path('AutomationOutputs/BattleRl/20261003_original_artifact_choices_failure.json').write_text(json.dumps({'error':str(error),'seeds':seeds,'traces':traces},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
