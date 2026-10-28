"""Actual original Ring modes, physical banner attachment, and seeded random Horn/Gem Bag replay."""
import os
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
import json
from pathlib import Path
from train_ppo import Rules
from trajectory_audit import audit

env=Rules();seeds={};traces=[];checks=0
try:
    for seed in range(800):
        inventory=env.call(op='inspectInventory',seed=seed)['state']
        for id in ('items_004','items_000','items_009','items_011'):
            if id in inventory['hand'] and (id!='items_000' or inventory['units']):seeds.setdefault(id,seed)
        if len(seeds)==4:break
    assert len(seeds)==4,seeds
    def choose(kind,**fields):return next(i for i in s['legal'] if env.actions[i]['Kind']==kind and all(env.actions[i][k]==v for k,v in fields.items()))
    def step(index):
        global s
        n=env.step(index);traces[-1].append({'before':s['state'],'action':env.actions[index],'after':n['state'],'reward':n['reward']});s=n
    for mode in ('normal','burn'):
        s=env.reset(seeds['items_004']);traces.append([]);before=s['state'];step(choose('artifact',Card='items_004',Choice=mode))
        if mode=='normal':
            assert s['state']['fame']==before['fame']+1;checks+=1
            prior=before['crystals'].get('Red',0)
            assert s['state']['crystals']['Red']==min(3,prior+1);checks+=1
            assert s['state']['tokens'].get('Red',0)-before['tokens'].get('Red',0)==1+(prior==3);checks+=1
        else:
            assert set(s['state']['artifactFlags']['infinite'])=={'Red','Black'};checks+=1
            assert s['state']['artifactFlags']['famePerSpell'].get('Red')==1;checks+=1
    s=env.reset(seeds['items_000']);traces.append([]);host=next(c for c in s['state']['zones']['hand'] if c['id']=='items_000');step(choose('artifact',Card='items_000',Choice='normal',Targets=0))
    assert s['state']['units'][0]['banner']=='items_000' and s['state']['zones']['banners']==[host];checks+=1
    for id in ('items_009','items_011'):
        results=[]
        for replay in range(2):
            s=env.reset(seeds[id]);traces.append([])
            if id=='items_009':step(choose('fight'))
            step(choose('artifact',Card=id,Choice='normal'));results.append(s['state'])
        assert results[0]==results[1] and len(results[0]['lastManaRolls'])==(1 if id=='items_009' else 2);checks+=1
    reports=[audit(t) for t in traces];assert not any(r['errors'] for r in reports),reports
    path=Path('AutomationOutputs/BattleRl/20261003_original_artifact_inputs.json')
    path.write_text(json.dumps({'scope':env.info['scope'],'original_source':'resources/text_json/items.json','seeds':seeds,'fixture_checks':checks,'reports':reports,'traces':traces},ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'seeds':seeds,'fixture_checks':checks,'transition_checks':sum(r['checks'] for r in reports),'errors':0}))
except Exception as error:
    Path('AutomationOutputs/BattleRl/20261003_original_artifact_inputs_failure.json').write_text(json.dumps({'error':str(error),'seeds':seeds,'traces':traces},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
