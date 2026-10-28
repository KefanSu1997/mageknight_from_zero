"""Real printed map movement/flight/site inputs, not PPO or scene acceptance."""
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
row=next(r for r in pool['tasks']+pool['unknown_tasks'] if 'hero_skill_pool_005:06' in r['task']['definition'].get('Build',{}).get('SkillIds',[]))
definition=copy.deepcopy(row['task']['definition']);definition['OriginalWorld']=True
definition['WorldSourceSha256']=hashlib.sha256(Path('resources/original_maps/tutorial_opening.json').read_bytes()).hexdigest()
definition['Build']['SkillIds']=['hero_skill_pool_005:05','hero_skill_pool_005:06']
env=Rules();trace=[];current=None
def step(index):
    global current
    following=env.step(index);trace.append(dict(before=current['state'],action=env.actions[index],after=following['state'],reward=following['reward']));current=following
def select(kind,q=None,r=None):
    return next(i for i in current['legal'] if env.actions[i]['Kind']==kind
                and (q is None or env.actions[i]['Targets']==q) and (r is None or env.actions[i]['Parts']==r))
try:
    found=False
    for seed in range(definition['ShuffleSeed'],definition['ShuffleSeed']+16):
        definition['ShuffleSeed']=seed;current=env.reset_task(definition);trace=[]
        moves=[i for i in current['legal'] if env.actions[i]['Kind']=='play' and env.actions[i]['Choice']=='strong'
               and env.actions[i]['Card'] in ('basic_card_000','basic_card_014')]
        if not moves:continue
        step(moves[0]);assert current['state']['movement']==4
        step(select('worldFly',1,-2));assert current['state']['movement']==2
        assert (current['state']['world']['q'],current['state']['world']['r'])==(1,-2)
        assert 'hero_skill_pool_005:06' in current['state']['usedSkillsRound']
        step(select('worldMove',2,-3));assert current['state']['movement']==0
        step(select('worldInteract'));assert current['state']['world']['inNegotiation']
        before=copy.deepcopy(current['state']);skill=select('skill');skill=next(i for i in current['legal'] if env.actions[i]['Kind']=='skill' and env.actions[i]['Card']=='hero_skill_pool_005:05')
        step(skill);assert current['state']['influence']==before['influence']+4
        healing=current['state']['healing'];influence=current['state']['influence']
        step(select('worldHealingBuy'));assert current['state']['influence']==influence-3
        assert current['state']['healing']==healing+1
        step(select('worldInteractEnd'));assert current['state']['world']['interactionClosed']
        found=True;break
    assert found,'No original legal fixture found in bounded16 shuffle streams'
    (out/'trace.json').write_text(json.dumps(dict(definition=definition,trace=trace),ensure_ascii=False,indent=2),encoding='utf-8')
    (out/'report.json').write_text(json.dumps(dict(status='printed21_cell_world_formal_inputs_checked_not_PPO',operations=len(trace),cells=len(current['state']['world']['cells']),features=len(current['features']),actions=len(env.actions),gaps=['special Loyalty recruitment','rampaging goal integration','all world cost/consumers','model/pool migration','Unity acceptance']),indent=2),encoding='utf-8')
    print('actual original4 movement→flight consumes2→village move consumes2→interaction→fluorescence4 checked')
except Exception as failure:
    (out/'failure.json').write_text(json.dumps(dict(reason=repr(failure),definition=definition,current=current,trace=trace),ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
