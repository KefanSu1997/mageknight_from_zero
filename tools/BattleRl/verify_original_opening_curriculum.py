"""Seven formal original openings plus family-aware readonly curriculum selection."""
import os,json,time
from pathlib import Path
from collections import Counter
os.environ['BATTLE_RL_ORIGINAL_OPENINGS']='1';os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
from train_ppo import Rules
from trajectory_audit import audit
from coverage_sampler import CoverageSampler
from policy_diagnostics import summarize_policy_trace
heroes=['Arythea','Tovak','Goldyx','Norowas','Wolfhawk','Krang','Braevalar'];unique=[(24,13),(20,27),(6,19),(12,5),(10,17),(3,26),(4,18)];all_unique={f'basic_card_{i:03}' for pair in unique for i in pair}
env=Rules();traces=[];checks=0

def step(i):
 global s
 n=env.step(i);traces[-1].append({'before':s['state'],'action':env.actions[i],'after':n['state'],'reward':n['reward']});s=n

def action(kind):return next(i for i in s['legal'] if env.actions[i]['Kind']==kind)
try:
 for seed,hero in enumerate(heroes):
  s=env.reset(seed);assert len(s['features'])==len(env.info['featureNames']);checks+=1;traces.append([]);b=s['state'];cards=[c for zone in b['zones'].values() for c in zone if not c['wound']]
  assert b['setup']=='original_level_one_start' and b['hero']==hero and b['level']==1 and b['fame']==0;checks+=1
  assert len(cards)==16 and len(b['hand'])==5 and b['deck']==11 and all(c['id'].startswith('basic_card_') for c in cards);checks+=1
  assert {c['id'] for c in cards}&all_unique=={f'basic_card_{i:03}' for i in unique[seed]};checks+=1
  assert not b['units'] and not b['skills'] and not b['publicSkills'] and not b['crystals'] and not b['tokens'];checks+=1
  step(action('fight'))
  step(action('advance'));assert s['state']['phase']=='Block'
  available=next(i for i in s['legal'] if env.actions[i]['Kind']=='sideways');old=s['state'];step(available)
  assert len(s['state']['blocks'])==1 and s['state']['blocks'][0]['value']==1 and s['state']['blocks'][0]['card']==env.actions[available]['Card'];checks+=1
  while s['state']['phase']!='End':step(action('advance'))
  step(action('nextTurn'));assert s['state']['hero']==hero and s['state']['turns']==2;checks+=1
 reports=[audit(t) for t in traces];assert all(not r['errors'] for r in reports),reports
 # Candidate inspection and selection must leave an active opening intact.
 midgame=env.reset(7);assert len(midgame['features'])==len(env.info['featureNames']);checks+=1
 s=env.reset(0);prior=s['state'];sampler=CoverageSampler(0,28);selected=[]
 for _ in range(14):seed,row=sampler.select(env);selected.append(row)
 observed=env.call(op='state');assert observed['state']==prior and observed['legal']==s['legal'] and observed['features']==s['features'];checks+=1
 counts=sampler.report();assert len(counts['selected_episodes_by_hero'])==7 and min(counts['selected_inventory_families'].values())>=6 and len(counts['selected_inventory_families'])==2;checks+=1
 assert not counts['actual_original_uses'] and not counts['actual_modes'];checks+=1
 result={'scope':env.info['scope'],'fixture_checks':checks,'reports':reports,'traces':traces,'curriculum':counts,'selected':selected,'limits':'Seven real level-one openings and actual block-sideways/next-turn physical-card lifecycle; not PPO usage, every starting printed effect, map-site battles, or Unity acceptance.'}
 Path('AutomationOutputs/BattleRl/20261003_original_opening_curriculum.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps({'scope':env.info['scope'],'fixture_checks':checks,'audit_checks':sum(r['checks'] for r in reports),'curriculum':counts}))
except Exception as error:
 Path(f'AutomationOutputs/BattleRl/20261003_original_opening_failure_{time.time_ns()}.json').write_text(json.dumps({'error':str(error),'traces':traces},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
