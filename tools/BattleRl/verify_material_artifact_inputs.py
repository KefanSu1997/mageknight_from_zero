"""Actual Wisdom Book supply/payment and Staff base/burn inputs, with original-card consumers."""
import os,json
from pathlib import Path
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
from train_ppo import Rules
from trajectory_audit import audit,_action_colors,_spell_colors

env=Rules();cases={};traces=[];checks=0
def choose(kind,**fields):return next(i for i in s['legal'] if env.actions[i]['Kind']==kind and all(env.actions[i][k]==v for k,v in fields.items()))
def step(index):
    global s
    n=env.step(index);traces[-1].append({'before':s['state'],'action':env.actions[index],'after':n['state'],'reward':n['reward']});s=n
try:
    for seed in range(6000):
        state=env.call(op='inspectInventory',seed=seed)['state']
        hand=state['hand']
        if 'items_013' in hand:
            materials=[id for id in hand if id!='items_013']
            for mode,gained,offer,color in [('normal','advanced_card_000','advancedOffer','Red'),('burn','magic_009','spellOffer','Red')]:
                slot=next((i for i,id in enumerate(materials[:8]) if _action_colors.get(id)==color),None)
                if gained in state[offer] and slot is not None:cases.setdefault('book_'+mode,(seed,slot,gained))
        if 'items_021' in hand:
            materials=[id for id in hand if id!='items_021']
            slot=next((i for i,id in enumerate(materials[:8]) if (_action_colors|_spell_colors).get(id)=='Blue'),None)
            if slot is not None:cases.setdefault('staff_normal',(seed,slot))
            cases.setdefault('staff_burn',(seed,))
        if len(cases)==4:break
    assert len(cases)==4,cases
    for mode in ('normal','burn'):
        seed,slot,gained=cases['book_'+mode];s=env.reset(seed);traces.append([])
        selected='materialSlot='+str(slot)+';'+('advanced=' if mode=='normal' else 'spells=')+gained
        step(choose('artifact',Card='items_013',Choice=mode,Input=selected))
        assert gained in s['state']['hand'] and s['state']['acquired'][-1]==gained;checks+=1
        if mode=='burn':step(choose('fight'))
        play=next(i for i in s['legal'] if env.actions[i]['Kind']=='play' and env.actions[i]['Card']==gained and env.actions[i]['Choice']=='base')
        before=s['state'];step(play)
        assert any(c['id']==gained for c in s['state']['zones']['played']);checks+=1
        if mode=='normal':assert s['state']['crystals'].get('Red',0)>=before['crystals'].get('Red',0);checks+=1
        else:
            assert any(p['card']==gained and p['value']==5 and p['element']=='Fire' and p['type']=='Ranged' for p in s['state']['attacks']);checks+=1
            stock=lambda state:state['tokens'].get('Red',0)+state['crystals'].get('Red',0)
            assert stock(before)-stock(s['state'])==1;checks+=1
    seed,slot=cases['staff_normal'];s=env.reset(seed);traces.append([])
    step(choose('artifact',Card='items_021',Choice='normal',Input=f'materialSlot={slot};first=1;second=0;color=0'))
    assert s['state']['crystals'].get('Red',0)>=2;checks+=1
    s=env.reset(cases['staff_burn'][0]);traces.append([])
    step(choose('artifact',Card='items_021',Choice='burn',Input='first=1;second=3;color=1'))
    assert s['state']['crystals'].get('Blue',0)>=2 and s['state']['healing']==3;checks+=1
    reports=[audit(t) for t in traces];assert not any(r['errors'] for r in reports),reports
    result={'scope':env.info['scope'],'cases':cases,'fixture_checks':checks,'transition_checks':sum(r['checks'] for r in reports),'reports':reports,'traces':traces,'limitations':'Staff white map path and Tome/Headband/Box specialized inputs remain pending; backend evidence only'}
    Path('AutomationOutputs/BattleRl/20261003_material_artifact_inputs.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:result[k] for k in ('scope','cases','fixture_checks','transition_checks')},ensure_ascii=False))
except Exception as error:
    Path('AutomationOutputs/BattleRl/20261003_material_artifact_inputs_failure.json').write_text(json.dumps({'error':str(error),'cases':cases,'traces':traces},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
