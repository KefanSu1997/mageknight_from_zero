"""Replay a real PPO toggle trajectory; only declared training fields/reward change."""
import os,json,hashlib
from pathlib import Path
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
from train_ppo import Rules
from trajectory_audit import audit
source=Path('AutomationOutputs/BattleRl/20261003_active_banners_ppo/audit_2048_1.json')
saved=json.loads(source.read_text(encoding='utf-8'));assert saved['seed']==3376
env=Rules();trace=[];count=changed=0
def game(state):
    result={k:v for k,v in state.items() if k not in ('planningRun','roundWindow','returningBanners','offeredUnitUses','mysticBox','artifactSupply')}
    result['units']=[{k:v for k,v in u.items() if k!='bannerEffect'} for u in result['units']]
    return result
try:
    s=env.reset(saved['seed']);indices={a['Key']:i for i,a in enumerate(env.actions)}
    for i,t in enumerate(saved['trace']):
        assert game(s['state'])==game(t['before']),('before',i)
        index=indices[t['action']['Key']];assert index in s['legal'],('legal',i)
        a=t['action'];planning=a['Kind'] in ('unitDamage','defend','clearDefend','bowChoice','killCrystal','protectionCleanup') or a['Kind']=='bannerUse' and a['Card']=='items_018'
        count=count+1 if planning else 0
        n=env.step(index)
        assert game(n['state'])==game(t['after']),('after',i)
        assert n['state']['planningRun']==count
        assert abs(n['reward']-(t['reward']-(.01*min(count,32) if planning else 0)))<1e-9,('reward',i)
        changed+=planning
        trace.append({'before':s['state'],'action':a,'after':n['state'],'reward':n['reward']});s=n
    report=audit(trace);assert not report['errors'],report
    result={'scope':env.info['scope'],'source':str(source),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
        'seed':saved['seed'],'operations':len(trace),'planning_rewards_changed':changed,'original_game_states_unchanged':True,
        'all_original_actions_still_legal':True,'report':report,'limits':'Reward consistency and original state replay; reduced PPO stalling is not yet proven'}
    Path('AutomationOutputs/BattleRl/20261003_box_nonbox_exact_replay.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='report'},ensure_ascii=False))
finally:env.close()
