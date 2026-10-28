"""Replay the real failure fixture against original crystal, fee and supply rules."""
import os
os.environ['BATTLE_RL_PROTOCOL']='--serve-journey-builds'
import json
from pathlib import Path
from train_ppo import Rules
from trajectory_audit import audit

env=Rules();cases=[]
try:
    for enhanced in (False,True):
        s=env.reset(2602);trace=[]
        def act(index):
            global s
            n=env.step(index);trace.append({'before':s['state'],'action':env.actions[index],'after':n['state'],'reward':n['reward']});s=n
        if enhanced:
            index=next(i for i in s['legal'] if env.actions[i]['Kind']=='source' and env.actions[i]['Option']==1)
            act(index)
        index=next(i for i in s['legal'] if env.actions[i]['Kind']=='play' and env.actions[i]['Card']=='advanced_card_033' and env.actions[i]['Choice']==('strong' if enhanced else 'base') and env.actions[i]['Input'].startswith('spells='))
        if enhanced:
            chosen=s['state']['spellOffer'][0]
            duplicate='spells='+chosen+','+chosen
            assert not any(env.actions[i]['Card']=='advanced_card_033' and env.actions[i]['Choice']=='strong' and env.actions[i]['Input']==duplicate for i in s['legal'])
        act(index);report=audit(trace)
        if report['errors']:
            Path('AutomationOutputs/BattleRl/20261003_spell_forge_oracle_fixture_failure.json').write_text(json.dumps({'enhanced':enhanced,'report':report,'trace':trace},ensure_ascii=False,indent=2),encoding='utf-8')
        assert not report['errors'],report
        assert not s['state']['acquired']
        cases.append({'enhanced':enhanced,'report':report,'trace':trace})
    out=Path('AutomationOutputs/BattleRl/20261003_spell_forge_original_oracle_checks.json')
    out.write_text(json.dumps({'scope':env.info['scope'],'seed':2602,'cases':cases},ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'operations':sum(len(c['trace']) for c in cases),'checks':sum(c['report']['checks'] for c in cases),'evidence':str(out)}))
finally:env.close()
