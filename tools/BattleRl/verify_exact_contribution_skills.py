"""Native Bullseye and Soul Guide modify one actual contribution, then consume it."""
import os,json,time
from pathlib import Path
from copy import deepcopy
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
from train_ppo import Rules
from trajectory_audit import audit
env=Rules();traces=[];fixtures=[]
def step(i):
 global state
 n=env.step(i);traces[-1].append({'before':state['state'],'action':env.actions[i],'after':n['state'],'reward':n['reward']});state=n
def choose(kind):return next(i for i in state['legal'] if env.actions[i]['Kind']==kind)
def start(seed,phase):
 global state
 state=env.reset(seed);traces.append([]);step(choose('fight'))
 while state['state']['phase']!=phase:step(choose('advance'))
try:
 bull=[];soul=[]
 for seed in range(1400):
  if seed%7 not in (4,5):continue
  b=env.call(op='inspectInventory',seed=seed)['state']
  if 'hero_skill_pool_004:04' in b['skills']:bull.append(seed)
  if 'hero_skill_pool_003:00' in b['skills']:soul.append(seed)
  if len(bull)>=15 and len(soul)>=12:break
 for phase in ('Ranged','Melee'):
  for seed in bull:
   start(seed,phase)
   if phase=='Melee':
    if sum(c['id']!='w' for c in state['state']['zones']['hand'])<2:traces.pop();continue
    step(choose('sideways'));step(choose('sideways'))
   else:
    candidates=[i for i in state['legal'] if env.actions[i]['Kind']=='play' and ((env.actions[i]['Card']=='basic_card_007' and env.actions[i]['Choice']=='strong') or (env.actions[i]['Card']=='basic_card_010' and env.actions[i]['Choice']=='strong_ranged'))]
    if not candidates:traces.pop();continue
    step(candidates[0])
   index=next((i for i in state['legal'] if env.actions[i]['Kind']=='skillBullseye' and env.actions[i]['Parts']==0),None)
   if index is None:traces.pop();continue
   before=state['state'];step(index)
   assert state['state']['attacks'][0]['value']==before['attacks'][0]['value']+(1 if phase=='Ranged' else 2)
   assert state['state']['attacks'][1:]==before['attacks'][1:] and not any(env.actions[i]['Kind']=='skillBullseye' for i in state['legal'])
   consumers=[i for i in state['legal'] if env.actions[i]['Kind']=='attack' and env.actions[i]['Parts']&1]
   consumed=bool(consumers)
   if consumers:step(consumers[0])
   fixtures.append({'skill':'Bullseye','phase':phase,'seed':seed,'actual_attack_consumed':consumed});break
  else:raise AssertionError(('no_actual_Bullseye_branch',phase))
 for seed in soul:
  state=env.reset(seed);traces.append([])
  index=next((i for i in state['legal'] if env.actions[i]['Kind']=='skill' and env.actions[i]['Card']=='hero_skill_pool_003:00'),None)
  if index is None:traces.pop();continue
  before=state['state'];step(index);assert state['state']['movement']==before['movement']+1 and state['state']['soulGuideBlockPending']
  step(choose('fight'));step(choose('advance'));step(choose('sideways'));step(choose('sideways'))
  before=state['state'];index=next(i for i in state['legal'] if env.actions[i]['Kind']=='soulGuideBlock' and env.actions[i]['Parts']==0);step(index)
  assert state['state']['blocks'][0]['value']==before['blocks'][0]['value']+1 and state['state']['blocks'][1:]==before['blocks'][1:]
  assert not state['state']['soulGuideBlockPending'] and not any(env.actions[i]['Kind']=='soulGuideBlock' for i in state['legal'])
  consumers=[i for i in state['legal'] if env.actions[i]['Kind']=='block']
  while not consumers and any(env.actions[i]['Kind']=='sideways' for i in state['legal']):
   step(choose('sideways'));consumers=[i for i in state['legal'] if env.actions[i]['Kind']=='block']
  if not consumers:continue
  step(consumers[0]);fixtures.append({'skill':'SoulGuide','seed':seed,'actual_block_consumed':True});break
 else:raise AssertionError('No actual Soul Guide consumer branch')
 assert any(f.get('actual_attack_consumed') for f in fixtures),'Need actual boosted attack consumer'
 assert fixtures[-1]['actual_block_consumed'],'Need actual boosted block consumer'
 reports=[audit(t) for t in traces];assert not any(r['errors'] for r in reports),reports
 bad=deepcopy(next(t for t in traces if any(x['action']['Kind']=='skillBullseye' and len(x['before']['attacks'])>=2 for x in t)))
 x=next(x for x in bad if x['action']['Kind']=='skillBullseye');x['after']['attacks'][1]['value']+=2;assert audit(bad)['errors']
 result={'scope':env.info['scope'],'fixtures':fixtures,'reports':reports,'trace':traces,'negative_checks':['boost_other_contribution','same_turn_second_Bullseye','second_SoulGuide_permission_consumer'],'limits':'Two printed phase bonuses and one actual block permission; not all card elements/artifacts, unit-block origin, borrowed skills, every subsequent consumer, PPO coverage or Unity acceptance.'}
 Path('AutomationOutputs/BattleRl/20261004_exact_contribution_skills.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps({'fixtures':fixtures,'audit_checks':sum(r['checks'] for r in reports)}))
except Exception as error:
 Path(f'AutomationOutputs/BattleRl/20261004_exact_contribution_failure_{time.time_ns()}.json').write_text(json.dumps({'error':str(error),'trace':traces},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
