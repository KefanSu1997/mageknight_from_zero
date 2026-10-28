"""Replay the saved PPO failure prefix through formal actions after the zero-block fix."""
import json,hashlib
from pathlib import Path
from train_ppo import Rules
from trajectory_audit import audit

source=Path('AutomationOutputs/BattleRl/20261004_printed_original_v38_fresh_ppo/training_rule_failure.json')
saved=json.loads(source.read_text(encoding='utf-8'))
env=Rules();trace=[]
result={'source':str(source),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
        'seed':saved['seed'],'scope':'actual saved PPO prefix; not another PPO training or Unity acceptance'}
try:
    state=env.reset(saved['seed'])
    actions={a['Key']:i for i,a in enumerate(env.actions)}
    failing=saved['report']['errors'][0]['step']
    assert failing==26 and saved['trace'][failing]['action']['Kind']=='cumbersome'
    for old in saved['trace'][:failing+1]:
        index=actions[old['action']['Key']]
        assert index in state['legal'],old['action']['Key']
        after=env.step(index)
        trace.append({'before':state['state'],'action':env.actions[index],'after':after['state']})
        state=after
    enemy=state['state']['enemies'][0]
    assert enemy['id']=='monster_048'
    assert enemy['attacks'][1]['effectiveAttack']==0 and enemy['attacks'][1]['blocked']
    assert not enemy['attacks'][0]['blocked'] and enemy['attacks'][2]['blocked']
    repeated=actions[saved['trace'][27]['action']['Key']]
    assert repeated not in state['legal'],'Already zero-blocked attack must not consume a second block'
    before=state['state']
    try:env.step(repeated)
    except RuntimeError as error:result['repeated_block_denied']=str(error)
    else:raise AssertionError('Duplicate zero block accepted')
    assert env.call(op='state')['state']==before,'Rejected duplicate block changed physical resources'
    checked=audit(trace);assert not checked['errors'],checked
    result.update(status='passed',audit=checked,trace=trace)
    print(json.dumps({k:v for k,v in result.items() if k!='trace'},ensure_ascii=False))
except Exception as error:
    result.update(status='failed',error=str(error),trace=trace)
    raise
finally:
    Path('AutomationOutputs/BattleRl/20261004_v39_native_zero_block_failure_replay.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    env.close()
