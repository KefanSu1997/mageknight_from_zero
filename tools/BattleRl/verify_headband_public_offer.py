"""Headband uses actual public offer from a saved PPO prefix with formal level reward."""
import os,json,time,hashlib
from pathlib import Path
from copy import deepcopy
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
from train_ppo import Rules
from trajectory_audit import audit
source=Path('AutomationOutputs/BattleRl/20261003_coverage_offered_units_ppo/audit_10240_4.json')
saved=json.loads(source.read_text(encoding='utf-8'));env=Rules();traces=[];cases={};checks=0

def matches(actual,expected):
 if isinstance(expected,dict):return all(k in actual and matches(actual[k],v) for k,v in expected.items())
 if isinstance(expected,list):return len(actual)==len(expected) and all(matches(a,b) for a,b in zip(actual,expected))
 return actual==expected

def start():
 global s
 s=env.reset(saved['seed']);traces.append([]);ids={a['Key']:i for i,a in enumerate(env.actions)}
 for ordinal,row in enumerate(saved['trace'][:72]):
  assert matches(s['state'],row['before']),('prefix_before',ordinal)
  i=ids[row['action']['Key']];assert i in s['legal'];step(i)
  assert matches(s['state'],row['after']) and s['reward']==row['reward'],('prefix_after',ordinal)
 assert s['state']['publicSkills']==['hero_skill_pool_003:01']
def action(kind,**kw):return next(i for i in s['legal'] if env.actions[i]['Kind']==kind and all(env.actions[i][k]==v for k,v in kw.items()))
def step(i):
 global s
 n=env.step(i);traces[-1].append({'before':s['state'],'action':env.actions[i],'after':n['state'],'reward':n['reward']});s=n
try:
 for mode in ('normal','burn'):
  start();host=next(c for c in s['state']['zones']['hand'] if c['id']=='items_022');prior=s['state']
  if mode=='burn':
   step(action('artifact',Card='items_022',Choice='burn'));assert 'hero_skill_pool_003:01' in s['state']['skills'] and not s['state']['publicSkills'];checks+=1
  step(action('fight'));step(action('advance'));step(action('advance'));assert s['state']['phase']=='AssignDamage';checks+=1
  if mode=='normal':
   step(action('artifact',Card='items_022',Choice='normal',Targets=0,Parts=0,Option=0));assert s['state']['skills']==prior['skills'] and s['state']['publicSkills']==prior['publicSkills'];checks+=1
  else:
   step(action('skill',Card='hero_skill_pool_003:01',Targets=0,Parts=0,Option=0));assert 'hero_skill_pool_003:01' in s['state']['usedSkillsTurn'];checks+=1
  hardened=s['state']['hardened'];assert hardened['physical']==2 and hardened['enemy']==s['state']['enemies'][0]['instance'];checks+=1
  step(action('advance'));assert s['state']['phase']=='Melee';checks+=1
  cases[mode]={'physical':host['instance'],'finalWounds':s['state']['wounds'],'hardened':hardened,'operations':len(traces[-1])}
 reports=[audit(t) for t in traces];assert not any(r['errors'] for r in reports),reports
 negatives=0
 for field in ('publicSkills','skills','hardened'):
  bad=deepcopy(traces[0]);row=next(t for t in bad if t['action']['Kind']=='artifact' and t['action']['Card']=='items_022')
  if field=='publicSkills':row['after'][field]=[]
  elif field=='skills':row['after'][field]=row['before'][field]+['hero_skill_pool_003:01']
  else:row['after'][field]['physical']=4
  assert audit(bad)['errors'],field;negatives+=1
 result={'scope':env.info['scope'],'source':str(source),'sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'seed':saved['seed'],'prefix_operations':72,'fixture_checks':checks,'negative_checks':negatives,'cases':cases,'reports':reports,'traces':traces,'limits':'Native Headband normal Battle Hardened and burn then actual own-skill consumer; specialized pending, Box alias and other public skills not independently validated; not PPO coverage or Unity acceptance.'}
 Path('AutomationOutputs/BattleRl/20261003_headband_real_public_offer.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps({k:result[k] for k in ('scope','fixture_checks','negative_checks','cases')}))
except Exception as e:
 Path(f'AutomationOutputs/BattleRl/20261003_headband_failure_{time.time_ns()}.json').write_text(json.dumps({'error':str(e),'traces':traces},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
