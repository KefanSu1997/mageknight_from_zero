"""Original assigned banners: actual unit activation, damage and later reuse."""
import os,json
from pathlib import Path
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
from train_ppo import Rules
from trajectory_audit import audit
env=Rules();traces=[];cases={};checks=0
unit_data={u['id']:u for name in ('army','army_2') for u in json.loads(Path('resources/text_json/'+name+'.json').read_text(encoding='utf-8'))}
def choose(kind,**fields):return next(i for i in s['legal'] if env.actions[i]['Kind']==kind and all(env.actions[i][k]==v for k,v in fields.items()))
def step(index):
    global s
    n=env.step(index);traces[-1].append({'before':s['state'],'action':env.actions[index],'after':n['state'],'reward':n['reward']});s=n
try:
    for seed in range(4000):
        inv=env.call(op='inspectInventory',seed=seed)['state']
        if not inv['units']:continue
        for id in ('items_001','items_003','items_018','items_002'):
            if id in inv['hand'] and (id!='items_018' or not unit_data[inv['units'][0]['id']].get('resistances') and not any('Assassination' in e['abilities'] for e in inv['enemies'])):cases.setdefault(id,seed)
        if len(cases)==4:break
    assert len(cases)==4,cases
    s=env.reset(cases['items_001']);traces.append([])
    step(choose('artifact',Card='items_001',Choice='normal',Targets=0));step(choose('fight'));step(choose('advance'))
    before=s['state'];step(choose('bannerUse',Card='items_001',Targets=0,Parts=0,Option=0))
    assert s['state']['enemies'][0]['attacks'][0]['cancelled'] and not s['state']['enemies'][0]['attacks'][0]['blocked'];checks+=1
    assert s['state']['fame']==before['fame']+1 and not s['state']['units'][0]['ready'];checks+=1
    assert not any(env.actions[i]['Kind']=='bannerUse' and env.actions[i]['Card']=='items_001' for i in s['legal']);checks+=1
    s=env.reset(cases['items_018']);traces.append([])
    step(choose('artifact',Card='items_018',Choice='normal',Targets=0));step(choose('fight'));step(choose('advance'));step(choose('advance'))
    step(choose('bannerUse',Card='items_018',Targets=0,Option=1));assert s['state']['units'][0]['fortitudeSelected'];checks+=1
    step(choose('unitDamage',Choice='0',Targets=0,Parts=0));step(choose('advance'))
    assert s['state']['units'][0]['bannerSpent'] and not s['state']['units'][0]['fortitudeSelected'];checks+=1
    # Find a real outside unit ability that exhausts the Courage-bearing unit.
    found=False
    for seed in range(4000):
        inv=env.call(op='inspectInventory',seed=seed)['state']
        if 'items_003' not in inv['hand']:continue
        s=env.reset(seed)
        choices=[i for i in s['legal'] if env.actions[i]['Kind']=='unit']
        if not choices:continue
        action=env.actions[choices[0]];unit=next(j for j,u in enumerate(s['state']['units']) if u['id']==action['Card'])
        traces.append([]);step(choose('artifact',Card='items_003',Choice='normal',Targets=unit))
        possible=[i for i in s['legal'] if env.actions[i]['Key']==action['Key']]
        if not possible:traces.pop();continue
        step(possible[0])
        if s['state']['units'][unit]['ready']:traces.pop();continue
        wounds=s['state']['units'][unit]['wounds'];step(choose('bannerUse',Card='items_003',Targets=unit))
        assert s['state']['units'][unit]['ready'] and s['state']['units'][unit]['wounds']==wounds;checks+=1
        step(choose('unit',Card=action['Card'],Choice=action['Choice'],Targets=action['Targets'],Parts=action['Parts'],Option=action['Option'],Input=action['Input']))
        assert not s['state']['units'][unit]['ready'] and not any(env.actions[i]['Kind']=='bannerUse' and env.actions[i]['Card']=='items_003' for i in s['legal']);checks+=1
        cases['courage_consumer']={'seed':seed,'unit':action['Card']};found=True;break
    assert found,'No actual Courage ready/reuse consumer found'
    for remove in (False,True):
        s=env.reset(cases['items_002']);traces.append([])
        step(choose('artifact',Card='items_002',Choice='burn'));step(choose('fight'))
        step(choose('advance'));step(choose('advance'));step(choose('advance'))
        wounds={c['instance'] for c in s['state']['zones']['hand'] if c['wound']};assert wounds,'Expected actual enemy damage wounds'
        step(choose('advance'));step(choose('protectionCleanup',Option=0))
        if remove:step(choose('protectionCleanup',Option=1))
        step(choose('nextTurn'))
        owned={c['instance'] for name,zone in s['state']['zones'].items() if name!='removed' for c in zone}
        assert (not wounds&owned) if remove else wounds<=owned;checks+=1
        if remove:assert wounds<={c['instance'] for c in s['state']['zones']['removed']};checks+=1
        assert not s['state']['artifactFlags']['protectionBurned'] and not s['state']['artifactFlags']['removeNewWounds'];checks+=1
    reports=[audit(t) for t in traces];assert not any(r['errors'] for r in reports),reports
    result={'scope':env.info['scope'],'cases':cases,'fixture_checks':checks,'reports':reports,'traces':traces,'limits':'Round return window is not yet exposed to the policy; backend evidence only'}
    Path('AutomationOutputs/BattleRl/20261003_active_banners.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'cases':cases,'fixture_checks':checks,'transition_checks':sum(r['checks'] for r in reports)}))
except Exception as error:
    import time
    Path(f'AutomationOutputs/BattleRl/20261003_active_banners_failure_{time.time_ns()}.json').write_text(json.dumps({'error':str(error),'cases':cases,'traces':traces},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
