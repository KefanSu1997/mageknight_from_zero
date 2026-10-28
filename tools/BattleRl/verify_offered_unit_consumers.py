"""Real Battle Summons permission/ability and Tome Honor Summons recruitment."""
import os,json
from pathlib import Path
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
from train_ppo import Rules
from trajectory_audit import audit,_action_colors,_spell_colors
env=Rules();traces=[];cases={};checks=0
def choose(kind,**fields):return next(i for i in s['legal'] if env.actions[i]['Kind']==kind and all(env.actions[i][k]==v for k,v in fields.items()))
def step(index):
    global s
    n=env.step(index);traces[-1].append({'before':s['state'],'action':env.actions[index],'after':n['state'],'reward':n['reward']});s=n
try:
    for seed in range(10000):
        inv=env.call(op='inspectInventory',seed=seed)['state']
        weak_enemy=any(e['armor']<=2 and not set(e['abilities'])&{'PhysicalResist','Defend','Elusive'} for e in inv['enemies']) and not any('Defend' in e['abilities'] for e in inv['enemies'])
        if 'magic_020' in inv['hand'] and inv['crystals'].get('White',0)>0 and 'army_2_001' in inv['unitOffer'] and weak_enemy:cases.setdefault('base',seed)
        if 'items_023' in inv['hand'] and 'magic_020' in inv['spellOffer']:
            materials=[id for id in inv['hand'] if id!='items_023']
            slot=next((i for i,id in enumerate(materials[:8]) if (_action_colors|_spell_colors).get(id)=='White'),None)
            if slot is not None:cases.setdefault('tome',(seed,slot))
        if len(cases)==2:break
    assert len(cases)==2,cases
    s=env.reset(cases['base']);traces.append([]);before=s['state']
    step(choose('play',Card='magic_020',Choice='base'))
    assert s['state']['offeredUnitUses']==1 and s['state']['crystals'].get('White',0)==before['crystals']['White']-1;checks+=1
    step(choose('fight'));step(choose('advance'));step(choose('advance'));step(choose('advance'))
    before=s['state'];options=[i for i in s['legal'] if env.actions[i]['Kind']=='offeredUnit']
    assert options,'Original permission has no actual ability consumer'
    # Pick a printed attack ability rather than a targetless support branch.
    original={u['id']:u for name in ('army','army_2') for u in json.loads(Path('resources/text_json/'+name+'.json').read_text(encoding='utf-8'))}
    index=next(i for i in options if env.actions[i]['Card']=='army_2_001' and env.actions[i]['Choice']=='0' and env.actions[i]['Option']==0)
    step(index)
    assert s['state']['offeredUnitUses']==0 and s['state']['units']==before['units'] and s['state']['unitOffer']==before['unitOffer'];checks+=1
    assert len(s['state']['attacks'])>len(before['attacks']);checks+=1
    assert not any(env.actions[i]['Kind']=='offeredUnit' for i in s['legal']);checks+=1
    attacks=[i for i in s['legal'] if env.actions[i]['Kind']=='attack'];assert attacks,'Actual supplied attack has no legal combat consumer'
    step(attacks[0]);assert len(s['state']['attacks'])<len(traces[-1][-1]['before']['attacks']);checks+=1
    seed,slot=cases['tome'];s=env.reset(seed);traces.append([]);before=s['state']
    prefix=f'materialSlot={slot};spellId=magic_020;spellChoice=strong;offeredUnit='
    index=next(i for i in s['legal'] if env.actions[i]['Kind']=='artifact' and env.actions[i]['Card']=='items_023' and env.actions[i]['Choice']=='burn' and env.actions[i]['Input'].startswith(prefix))
    action=env.actions[index];fields=dict(v.split('=',1) for v in action['Input'].split(';'));offered=fields['offeredUnit'];step(index)
    assert offered in before['unitOffer'] and offered not in s['state']['unitOffer'] and any(u['id']==offered for u in s['state']['units']);checks+=1
    assert len(s['state']['units'])<=s['state']['commandSlots'] and s['state']['tokens']==before['tokens'] and s['state']['crystals']==before['crystals'];checks+=1
    assert s['state']['dayPart']=='Day' and s['state']['spellOffer']==before['spellOffer'];checks+=1
    step(choose('fight'));step(choose('advance'));step(choose('advance'));step(choose('advance'))
    possible=[i for i in s['legal'] if env.actions[i]['Kind']=='unit' and env.actions[i]['Card']==offered];assert possible,'New actual recruited unit has no consumer'
    step(possible[0]);checks+=1
    reports=[audit(t) for t in traces];assert not any(r['errors'] for r in reports),reports
    result={'scope':env.info['scope'],'fixture_checks':checks,'cases':cases,'reports':reports,'traces':traces,'limits':'Backend consumers; offered attack physical provenance and all special unit inputs are not completely attributed'}
    Path('AutomationOutputs/BattleRl/20261003_offered_unit_consumers.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'fixture_checks':checks,'cases':cases,'transition_checks':sum(r['checks'] for r in reports)}))
except Exception as error:
    import time
    Path(f'AutomationOutputs/BattleRl/20261003_offered_unit_consumers_failure_{time.time_ns()}.json').write_text(json.dumps({'error':str(error),'cases':cases,'traces':traces},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
