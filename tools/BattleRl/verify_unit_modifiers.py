"""Printed Altem Guards green resistance: actual fee, visible permission, real turn expiry."""
import os
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-parties')
import json
from pathlib import Path
from train_ppo import Rules
from trajectory_audit import audit

env=Rules();trace=[];seed=None
try:
    for candidate in range(150):
        s=env.reset(candidate)
        moves=[i for i in s['legal'] if env.actions[i]['Kind']=='unit' and env.actions[i]['Card']=='army_000' and env.actions[i]['Choice']=='2']
        if moves:seed=candidate;break
    assert seed is not None
    def step(index):
        global s
        n=env.step(index);trace.append({'before':s['state'],'action':env.actions[index],'after':n['state'],'reward':n['reward']});s=n
    def stock(state):return sum(state[zone].get(color,0) for zone in ('tokens','crystals') for color in ('Green','Gold'))
    before=s;step(moves[0]);assert s['state']['unitModifiers']['resistAll']
    assert stock(before['state'])-stock(s['state'])==1 and before['features']!=s['features']
    # A printed unit ability alone does not replace mandatory ordinary-card activity.
    rest=next(i for i in s['legal'] if env.actions[i]['Kind']=='rest' and env.actions[i]['Card']);step(rest)
    step(next(i for i in s['legal'] if env.actions[i]['Kind']=='nextTurn'))
    assert not s['state']['unitModifiers']['resistAll']
    report=audit(trace);assert not report['errors'],report
    Path('AutomationOutputs/BattleRl/20261003_original_unit_modifiers.json').write_text(json.dumps({'scope':env.info['scope'],'seed':seed,'source':'resources/text_json/army.json army_000 ability 2','fixture_checks':4,'report':report,'trace':trace},ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'seed':seed,'fixture_checks':4,'transition_checks':report['checks'],'errors':0}))
except Exception as error:
    Path('AutomationOutputs/BattleRl/20261003_original_unit_modifiers_failure.json').write_text(json.dumps({'seed':seed,'error':str(error),'trace':trace},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
