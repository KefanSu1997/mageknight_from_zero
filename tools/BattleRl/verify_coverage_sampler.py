"""Real inventory probes cannot reset or mutate the live rollout."""
import os,json
from pathlib import Path
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
from train_ppo import Rules
from coverage_sampler import CoverageSampler
env=Rules();checks=0
try:
    live=env.reset(3368);sampler=CoverageSampler(3400,21);rows=[]
    for _ in range(7):
        seed,row=sampler.select(env);rows.append(row)
        assert env.call(op='state')==live;checks+=1
        assert all(count==0 for count in row['previous_actual_uses'].values());checks+=1
    assert len(sampler.heroes)==7 and set(sampler.heroes.values())=={1};checks+=1
    assert len({row['seed'] for row in rows})==7 and not sampler.uses and not sampler.modes;checks+=1
    actual=json.loads(Path('AutomationOutputs/BattleRl/20261003_active_banners.json').read_text(encoding='utf-8'))
    trace=actual['traces'][0]
    for t in trace:sampler.observe(t['before'],t['action'],t['after'])
    assert sampler.uses['items_001']==2 and any(':bannerUse:' in mode for mode in sampler.modes);checks+=1
    prior=sampler.report();rejected=dict(trace[-1]['after'],stopReason='formal_test_denied')
    sampler.observe(trace[-1]['before'],trace[-1]['action'],rejected)
    assert sampler.report()==prior;checks+=1
    result={'fixture_checks':checks,'selections':rows,'actual_use_report':sampler.report(),'limits':'Seed selection and counter evidence, not PPO training or original-effect coverage'}
    Path('AutomationOutputs/BattleRl/20261003_coverage_sampler.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'fixture_checks':checks,'heroes':dict(sampler.heroes),'live_rollout_unchanged':True}))
finally:env.close()
