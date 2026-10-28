"""Exact replay of actual PPO Shocktroops audit failure, with independent negative order checks."""
import os,json,hashlib,time
from pathlib import Path
from copy import deepcopy
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
from train_ppo import Rules
from trajectory_audit import audit
source=Path('AutomationOutputs/BattleRl/20261003_coverage_offered_units_ppo/training_rule_failure.json')
saved=json.loads(source.read_text(encoding='utf-8'));assert saved['seed']==5303
env=Rules();trace=[];checks=0

def game(state):
 result={k:v for k,v in state.items() if k not in ('mysticBox','artifactSupply')}
 result['units']=[{k:v for k,v in u.items() if k!='bannerEffect'} for u in result['units']]
 result['enemies']=[dict(e,attacks=[{k:v for k,v in a.items() if k not in ('forcedUnit','effectiveAttack')} for a in e['attacks']]) for e in result['enemies']]
 return result
try:
 s=env.reset(saved['seed']);ids={a['Key']:i for i,a in enumerate(env.actions)}
 for i,t in enumerate(saved['trace']):
  assert game(s['state'])==game(t['before']),('before',i)
  index=ids[t['action']['Key']];assert index in s['legal'],('legal',i)
  n=env.step(index);assert game(n['state'])==game(t['after']) and n['reward']==t['reward'],('after',i)
  trace.append({'before':s['state'],'action':t['action'],'after':n['state'],'reward':n['reward']});s=n;checks+=1
 report=audit(trace);assert not report['errors'],report
 row=trace[46];attack=row['before']['enemies'][0]['attacks'][0];assert attack['forcedUnit']=='army_2_010' and attack['effectiveAttack']>0;checks+=1
 assert row['after']['unitDamagePlan'][0]['units']==['army_2_010','army_2_001'];checks+=1
 for actual in (['army_2_001','army_2_010'],['army_2_010'],['army_2_010','army_2_000']):
  bad=deepcopy(trace);bad[46]['after']['unitDamagePlan'][0]['units']=actual
  assert any('exact selected order' in e['rule'] for e in audit(bad)['errors']);checks+=1
 result={'scope':env.info['scope'],'source':str(source),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'seed':saved['seed'],'fixture_checks':checks,'exact_original_states_rewards':True,'operations':len(trace),'report':report,'trace':trace,'limits':'Actual mandatory Shocktroops reorder; extra same-ID entities and all assassination combinations remain unverified'}
 Path('AutomationOutputs/BattleRl/20261003_forced_damage_exact_replay.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps({k:result[k] for k in ('scope','fixture_checks','exact_original_states_rewards','operations')}))
except Exception as e:
 Path(f'AutomationOutputs/BattleRl/20261003_forced_damage_failure_{time.time_ns()}.json').write_text(json.dumps({'error':str(e),'trace':trace},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
