"""Actual Norowas command marker recruitment at the printed original village."""
import copy
import hashlib
import json
import os
from pathlib import Path
import sys
os.environ.update(BATTLE_RL_ORIGINAL_WORLD='1',BATTLE_RL_SHARED_SKILLS='1',BATTLE_RL_SHAPESHIFT='1',
                  BATTLE_RL_NATIVE_PAYMENTS='1',BATTLE_RL_NATIVE_CONVERSIONS='1',BATTLE_RL_NATIVE_ENERGY_TARGETS='1',
                  BATTLE_RL_ORIGINAL_OPENINGS='1',BATTLE_RL_PROTOCOL='--serve-journey-artifacts')
from train_ppo import Rules
out=Path(sys.argv[1])
if out.exists():raise ValueError('Preserve evidence')
out.mkdir(parents=True)
pool=json.loads(Path('AutomationOutputs/BattleRl/20261008_shapeshift_rule_pool_rebinding/pool.json').read_text())
row=next(r for r in pool['tasks']+pool['unknown_tasks'] if 'hero_skill_pool_002:07' in r['task']['definition'].get('Build',{}).get('SkillIds',[]))
definition=copy.deepcopy(row['task']['definition']);definition['OriginalWorld']=True
definition['WorldSourceSha256']=hashlib.sha256(Path('resources/original_maps/tutorial_opening.json').read_bytes()).hexdigest()
definition['Build']['SkillIds']=['hero_skill_pool_002:07','hero_skill_pool_002:03'];definition['Build']['UnitIds']=definition['Build']['UnitIds'][:2]
env=Rules();current=None;trace=[]
def step(index):
    global current
    following=env.step(index);trace.append(dict(before=current['state'],action=env.actions[index],after=following['state'],reward=following['reward']));current=following
def find(kind,q=None,r=None):return next(i for i in current['legal'] if env.actions[i]['Kind']==kind and (q is None or env.actions[i]['Targets']==q) and (r is None or env.actions[i]['Parts']==r))
try:
    found=False
    for seed in range(definition['ShuffleSeed'],definition['ShuffleSeed']+24):
        definition['ShuffleSeed']=seed;current=env.reset_task(definition);trace=[]
        for _ in range(3):
            if current['state']['movement']>=6:break
            moves=[i for i in current['legal'] if env.actions[i]['Kind']=='play' and env.actions[i]['Card'] in ('basic_card_000','basic_card_007','basic_card_014') and env.actions[i]['Choice'] in ('base','strong') and not (env.actions[i]['Card']=='basic_card_007' and env.actions[i]['Choice']=='strong')]
            if not moves:break
            step(moves[-1])
        if current['state']['movement']<6:continue
        for q,r in [(0,-1),(1,-2),(2,-3)]:step(find('worldMove',q,r))
        step(find('worldInteract'))
        step(next(i for i in current['legal'] if env.actions[i]['Kind']=='skill' and env.actions[i]['Card']=='hero_skill_pool_002:03'))
        ordinary_recruits={env.actions[i]['Card'] for i in current['legal'] if env.actions[i]['Kind']=='worldRecruit'}
        before=copy.deepcopy(current['state']);step(next(i for i in current['legal'] if env.actions[i]['Kind']=='worldLoyaltySelect' and env.actions[i]['Option']==1))
        assert current['state']['world']['loyaltySelected'] and current['state']['units']==before['units']
        choices=[i for i in current['legal'] if env.actions[i]['Kind']=='worldRecruit']
        if not choices:raise ValueError('Actual offer/printed village/discount cannot recruit in this fixture')
        before=copy.deepcopy(current['state']);step(choices[0]);after=current['state'];bound=after['world']['loyaltyBound']
        assert bound and not after['world']['loyaltySelected'] and not after['world']['loyaltyAvailable']
        assert bound not in ordinary_recruits
        assert len(after['units'])==len(before['units'])+1 and all(u in after['units'] for u in before['units'])
        assert bound not in after['unitOffer']
        assert not any(env.actions[i]['Kind']=='worldDismiss' and env.actions[i]['Card']==bound for i in current['legal'])
        (out/'trace.json').write_text(json.dumps(dict(definition=definition,trace=trace),ensure_ascii=False,indent=2),encoding='utf-8')
        (out/'report.json').write_text(json.dumps(dict(status='actual_special_loyalty_recruit_not_frequency_activation',operations=len(trace),bound_unit=bound,influence_before=before['influence'],influence_after=after['influence'],gaps=['bound-unit cannot dismiss/use exception checks','four-plus unit observer','PPO/pool/checkpoint migration','Unity acceptance']),indent=2),encoding='utf-8');found=True;print('actual printed village loyalty recruit',bound);break
    assert found,'No bounded legal fixture'
except Exception as failure:
    (out/'failure.json').write_text(json.dumps(dict(reason=repr(failure),definition=definition,current=current,trace=trace),ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
