"""Pair actual original unit activations with/without the same owned Leadership."""
import os,json,time,hashlib
from pathlib import Path
from copy import deepcopy
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
from train_ppo import Rules
from trajectory_audit import audit,classify_attack_sources
env=Rules();skill='hero_skill_pool_002:06';traces=[];fixtures=[]
def step(index):
 global state
 n=env.step(index);traces[-1].append({'before':state['state'],'action':env.actions[index],'after':n['state'],'reward':n['reward']});state=n
def choose(kind):return next(i for i in state['legal'] if env.actions[i]['Kind']==kind)
def begin(seed,phase):
 global state
 state=env.reset(seed);traces.append([]);step(choose('fight'))
 while state['state']['phase']!=phase:step(choose('advance'))
try:
 candidates=[]
 for seed in range(3,3000,7):
  b=env.call(op='inspectInventory',seed=seed)['state']
  if skill in b['skills']:candidates.append(seed)
  if len(candidates)>=35:break
 for phase,bonus in [('Ranged',1),('Block',3),('Melee',2)]:
  for seed in candidates:
   begin(seed,phase)
   available=[i for i in state['legal'] if env.actions[i]['Kind']=='unitLeadership' and not env.actions[i]['Input']]
   if not available:traces.pop();continue
   # Melee may permit an original ranged activation, whose bonus is still +1.
   found=False
   for index in available:
    action=env.actions[index];baseline=dict(action,Kind='unit');baseline_key='unit'+action['Key'][len('unitLeadership'):]
    normal=next(i for i in state['legal'] if env.actions[i]['Key']==baseline_key)
    before=state['state'];step(normal);ordinary=state['state']
    values=sum(c['value'] for c in ordinary['blocks'])-sum(c['value'] for c in before['blocks']) if phase=='Block' else sum(c['value'] for c in ordinary['attacks'])-sum(c['value'] for c in before['attacks'])
    if phase=='Melee' and not any(c['unit']==action['Card'] and c['type']=='Melee' for c in ordinary['attacks']):
     begin(seed,phase);continue
    begin(seed,phase);assert state['state']==before
    step(index);boosted=state['state']
    actual=sum(c['value'] for c in boosted['blocks'])-sum(c['value'] for c in before['blocks']) if phase=='Block' else sum(c['value'] for c in boosted['attacks'])-sum(c['value'] for c in before['attacks'])
    assert actual==values+bonus,(phase,actual,values,action)
    profile=lambda state,key:[{k:v for k,v in c.items() if k not in ('value','effectiveValue')} for c in state[key]]
    assert profile(ordinary,'blocks')==profile(boosted,'blocks') and profile(ordinary,'attacks')==profile(boosted,'attacks'),('printed_element_type_and_actual_source_retained',phase)
    for field in ('tokens','crystals','source','held','units','zones'):assert ordinary[field]==boosted[field],('real_fee_and_unit_exhaustion',field)
    assert skill not in ordinary['usedSkillsTurn'] and skill in boosted['usedSkillsTurn']
    assert not any(env.actions[i]['Kind']=='unitLeadership' for i in state['legal']),('once_per_turn',phase)
    consumed=False
    if phase!='Block':
     parts=[j for j,c in enumerate(boosted['attacks']) if c['unit']==action['Card']]
     consumers=[i for i in state['legal'] if env.actions[i]['Kind']=='attack' and any(env.actions[i]['Parts']&(1<<j) for j in parts)]
     if consumers:step(consumers[0]);consumed=True
    fixtures.append({'seed':seed,'phase':phase,'unit_action':action,'base_value':values,'leadership_value':actual,'printed_bonus':bonus,'same_real_unit_fee_and_exhaustion':True,'actual_attack_consumed':consumed});found=True;break
   if found:break
  else:raise AssertionError(('missing_actual_leadership_branch',phase))
 reports=[audit(t) for t in traces];assert not any(r['errors'] for r in reports),reports
 bad=deepcopy(next(t for t in traces if any(x['action']['Kind']=='unitLeadership' for x in t)))
 t=next(x for x in bad if x['action']['Kind']=='unitLeadership');t['after']['usedSkillsTurn'].remove(skill);assert audit(bad)['errors']
 consumers=[r for t in traces for r in classify_attack_sources(t) if r['mode'].startswith('unit_leadership:')]
 assert consumers,'Need a subsequent actual Leadership attack consumer'
 result={'scope':env.info['scope'],'fixtures':fixtures,'actual_attack_consumers':consumers,'reports':reports,'trace':traces,'original_skill_text_sha256':hashlib.sha256(Path('resources/text_json/skill.json').read_bytes()).hexdigest(),'negative_checks':['missing_once_per_turn_consumption','same_turn_second_leadership'],'limits':'Three paired printed bonus branches with real identical unit fees/exhaustion; not all unit abilities, fee alternatives, Hero Leadership borrowing, PPO usage or Unity acceptance.'}
 Path('AutomationOutputs/BattleRl/20261004_native_leadership.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps({'scope':env.info['scope'],'fixtures':fixtures,'audit_checks':sum(r['checks'] for r in reports),'actual_attack_consumers':consumers}))
except Exception as error:
 Path(f'AutomationOutputs/BattleRl/20261004_native_leadership_failure_{time.time_ns()}.json').write_text(json.dumps({'error':str(error),'trace':traces},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
