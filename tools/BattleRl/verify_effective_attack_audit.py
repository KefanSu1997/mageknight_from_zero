"""Replay a new actual PPO trajectory and reject incorrect effective thresholds."""
import json,time
from pathlib import Path
from copy import deepcopy
from train_ppo import Rules
from trajectory_audit import audit

source=Path('AutomationOutputs/BattleRl/20261004_native_rally_ppo/first_actual_unit_attack_consumer.json')
x=json.loads(source.read_text(encoding='utf-8'));env=Rules();trace=[]
def legacy(state):
    state=deepcopy(state);state.pop('attackResistanceFlags',None)
    for enemy in state['enemies']:enemy.pop('effectiveArmor',None)
    for part in state['attacks']:part.pop('effectiveElement',None)
    return state
try:
    state=env.reset(x['seed']);lookup={a['Key']:i for i,a in enumerate(env.actions)}
    for i,t in enumerate(x['trace']):
        assert legacy(state['state'])==t['before'],('before',i)
        n=env.step(lookup[t['action']['Key']]);assert legacy(n['state'])==t['after'] and n['reward']==t['reward'],('after',i)
        trace.append({'before':state['state'],'action':t['action'],'after':n['state'],'reward':n['reward']});state=n
    report=audit(trace);assert not report['errors'],report
    assert trace[9]['before']['enemies'][0]['armor']==7 and trace[9]['before']['enemies'][0]['effectiveArmor']==6
    bad=deepcopy(trace);bad[9]['before']['enemies'][0]['effectiveArmor']=7
    rejected=audit(bad)['errors'];assert any(e['rule']=='selected attack totals resistance halves once and meets effective armor' for e in rejected)
    # Odd resisted contributions must be totaled before integer division.
    synthetic=deepcopy(trace[9]);synthetic['before']['enemies'][0]['effectiveArmor']=1
    synthetic['before']['enemies'][0]['effectiveResistances']=['Physical']
    for part in synthetic['before']['attacks']:part.update(effectiveElement='Physical',effectiveValue=1)
    assert not any(e['rule']=='selected attack totals resistance halves once and meets effective armor' for e in audit([synthetic])['errors'])
    synthetic['before']['enemies'][0]['effectiveArmor']=2
    assert any(e['rule']=='selected attack totals resistance halves once and meets effective armor' for e in audit([synthetic])['errors'])
    result={'source':str(source),'scope':env.info['scope'],'seed':x['seed'],'state_and_reward_unchanged_except_new_audit_metadata':True,'audit':report,'negative':rejected,'odd_resisted_aggregate_negative':True,'trace':trace,'limits':'observed armor/effective values arithmetic and consumers; not independent proof of every modifier, provisional defenders remain gaps, no PPO/UI acceptance'}
    Path('AutomationOutputs/BattleRl/20261004_effective_attack_audit.json').write_text(json.dumps(result,ensure_ascii=False),encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='trace'},ensure_ascii=False))
except Exception as e:
    Path(f'AutomationOutputs/BattleRl/20261004_effective_attack_audit_failure_{time.time_ns()}.json').write_text(json.dumps({'error':str(e),'trace':trace},ensure_ascii=False),encoding='utf-8');raise
finally:env.close()
