"""Original Tome payment, day strong spell, physical source and actual attack consumer."""
import os,json
from pathlib import Path
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
from train_ppo import Rules
from trajectory_audit import audit,classify_attack_sources,_action_colors,_spell_colors

env=Rules();cases={};traces=[];checks=0
def choose(kind,**fields):return next(i for i in s['legal'] if env.actions[i]['Kind']==kind and all(env.actions[i][k]==v for k,v in fields.items()))
def step(index):
    global s
    n=env.step(index);traces[-1].append({'before':s['state'],'action':env.actions[index],'after':n['state'],'reward':n['reward']});s=n
try:
    for seed in range(10000):
        inv=env.call(op='inspectInventory',seed=seed)['state']
        if 'items_023' not in inv['hand']:continue
        materials=[id for id in inv['hand'] if id!='items_023']
        for spell,color,element,resistance in [('magic_009','Red','Fire','FireResist'),('magic_015','Blue','Ice','IceResist')]:
            slot=next((i for i,id in enumerate(materials[:8]) if (_action_colors|_spell_colors).get(id)==color),None)
            enemy=inv['enemies'][0]
            if spell in inv['spellOffer'] and slot is not None and enemy['armor']<=5 and not set(enemy['abilities'])&{resistance,'Fortified','Elusive','Defend'} and not any('Defend' in e['abilities'] for e in inv['enemies']):cases.setdefault('attack',(seed,slot,spell,element))
        slot=next((i for i,id in enumerate(materials[:8]) if (_action_colors|_spell_colors).get(id)=='Blue'),None)
        if 'magic_013' in inv['spellOffer'] and slot is not None and len(inv['enemies'])>1:cases.setdefault('target',(seed,slot,'magic_013'))
        face=next((f for f in inv['source'] if f in ('Red','Blue','Green','White')),None)
        if 'magic_012' in inv['spellOffer'] and slot is not None and face is not None:cases.setdefault('deferred',(seed,slot,face))
        if len(cases)==3:break
    assert len(cases)==3,cases
    for mode,choice in [('normal','base'),('burn','strong')]:
        seed,slot,spell,element=cases['attack'];s=env.reset(seed);traces.append([]);step(choose('fight'));before=s['state']
        host=next(c for c in before['zones']['hand'] if c['id']=='items_023')
        step(choose('artifact',Card='items_023',Choice=mode,Input=f'materialSlot={slot};spellId={spell};spellChoice={choice}'))
        assert s['state']['tokens']==before['tokens'] and s['state']['crystals']==before['crystals'];checks+=1
        assert s['state']['spellOffer']==before['spellOffer'] and s['state']['dayPart']=='Day';checks+=1
        part=next(p for p in s['state']['attacks'] if p['cardInstance']==host['instance'])
        assert part['value']==(5 if mode=='normal' else 8) and part['element']==element and part['type']==('Ranged' if mode=='normal' else 'Siege');checks+=1
        assert s['state']['wounds']==before['wounds']+(mode=='burn');checks+=1
        index=s['state']['attacks'].index(part);step(choose('attack',Targets=1,Parts=1<<index))
        assert s['state']['kills']==before['kills']+1;checks+=1
        sources=classify_attack_sources(traces[-1]);assert any(p['card']=='items_023' and p['mode']==f'artifact:{mode}:{spell}:{choice}' for p in sources);checks+=1
    seed,slot,spell=cases['target'];s=env.reset(seed);traces.append([]);step(choose('fight'))
    before=s['features'];step(choose('artifact',Card='items_023',Choice='normal',Targets=1,Input=f'materialSlot={slot};spellId={spell};spellChoice=base'))
    assert s['state']['spellOffer']==traces[-1][-1]['before']['spellOffer'];checks+=1
    assert s['state']['enemies'][1]['skipped'] and not s['state']['enemies'][0]['skipped'] and s['features']!=before;checks+=1
    seed,slot,face=cases['deferred'];s=env.reset(seed);traces.append([]);before=s['state']
    step(choose('artifact',Card='items_023',Choice='normal',Input=f'materialSlot={slot};spellId=magic_012;spellChoice=base_deferred;dice={face};colors={face}'))
    assert s['state']['held'] is None and s['state']['roundClaimedDice']==[face];checks+=1
    assert s['state']['tokensPerTurn'].get(face,0)==before['tokensPerTurn'].get(face,0)+1 and s['state']['tokens']==before['tokens'] and s['state']['crystals']==before['crystals'];checks+=1
    step(choose('nextTurn'))
    assert s['state']['turns']==2 and s['state']['tokens'].get(face,0)==1 and s['state']['roundClaimedDice']==[face];checks+=1
    reports=[audit(t) for t in traces];assert not any(r['errors'] for r in reports),reports
    result={'scope':env.info['scope'],'cases':cases,'fixture_checks':checks,'transition_checks':sum(r['checks'] for r in reports),'reports':reports,'traces':traces,'limits':'Map/multiple-material/copy/response inputs are not all covered; backend evidence only'}
    Path('AutomationOutputs/BattleRl/20261003_spell_tome_inputs.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:result[k] for k in ('scope','cases','fixture_checks','transition_checks')},ensure_ascii=False))
except Exception as error:
    Path('AutomationOutputs/BattleRl/20261003_spell_tome_inputs_failure.json').write_text(json.dumps({'error':str(error),'cases':cases,'traces':traces},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
