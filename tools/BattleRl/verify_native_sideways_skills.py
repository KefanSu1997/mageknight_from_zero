"""Actual owned original skills, physical hand slots and formal native consumers."""
import os,json,time,hashlib
from pathlib import Path
from copy import deepcopy
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
from train_ppo import Rules
from trajectory_audit import audit,classify_attack_sources

ids=['hero_skill_pool_007:06','hero_skill_pool_007:07','hero_skill_pool_005:07']
env=Rules();traces=[];fixtures=[];seeds={k:[] for k in ids}
def step(index):
 global state
 after=env.step(index);traces[-1].append({'before':state['state'],'action':env.actions[index],'after':after['state'],'reward':after['reward']});state=after
def kind(name):return next(i for i in state['legal'] if env.actions[i]['Kind']==name)
try:
 # Readonly inspection selects legal inventories, without fabricating owned skills.
 for seed in range(700):
  if seed%7 not in (1,2):continue
  inventory=env.call(op='inspectInventory',seed=seed)['state']
  for skill in ids:
   if skill in inventory['skills']:seeds[skill].append(seed)
  if all(len(v)>=8 for v in seeds.values()):break
 for skill in ids:
  for use in ('Move','Influence','Block','Attack'):
   for seed in seeds[skill]:
    state=env.reset(seed);traces.append([])
    if use in ('Block','Attack'):
     step(kind('fight'));step(kind('advance'))
     if use=='Attack':
      while state['state']['phase']!='Melee':step(kind('advance'))
    actions=[i for i in state['legal'] if env.actions[i]['Kind']=='skillSideways' and env.actions[i]['Card']==skill and env.actions[i]['Choice']==use]
    if not actions:traces.pop();continue
    # Prefer the source promise and matching-color high value when available.
    actions.sort(key=lambda i:(env.actions[i]['Option']==1,env.actions[i]['Parts']),reverse=True)
    index=actions[0];before=state['state'];action=env.actions[index];physical=before['handOrder'][action['Parts']]
    step(index)
    assert not any(env.actions[i]['Kind']=='skillSideways' and env.actions[i]['Card']==skill for i in state['legal']),('once_per_turn',skill)
    if skill=='hero_skill_pool_007:07' and action['Option']==1:
     assert state['state']['forgoSource'] and not any(env.actions[i]['Kind'] in ('source','extraSource') for i in state['legal'])
    consumed=False
    if use=='Attack':
     part=next(j for j,c in enumerate(state['state']['attacks']) if c['cardInstance']==physical)
     actual=[i for i in state['legal'] if env.actions[i]['Kind']=='attack' and env.actions[i]['Parts']&(1<<part)]
     if actual:
      step(actual[0]);assert not any(c['cardInstance']==physical for c in state['state']['attacks']);consumed=True
    fixtures.append({'seed':seed,'skill':skill,'use':use,'physical':physical,'option':action['Option'],'actual_attack_consumed':consumed});break
   else:raise AssertionError(('no_actual_consumer',skill,use))
 reports=[audit(t) for t in traces]
 assert not any(r['errors'] for r in reports),reports
 consumers=[r for t in traces for r in classify_attack_sources(t) if r['mode'].startswith('skillSideways:')]
 assert consumers,'Need at least one subsequent real native sideways attack consumer'
 # Independent checker must reject a fabricated numerical contribution.
 t=next(t for t in traces if t[-1]['action']['Choice']=='Block');bad=deepcopy(t);physical=t[-1]['before']['handOrder'][t[-1]['action']['Parts']]
 next(c for c in bad[-1]['after']['blocks'] if c['cardInstance']==physical)['value']+=1
 assert audit(bad)['errors']
 # Source use before Magic Repel excludes the +3 promise but retains +2.
 seed=seeds['hero_skill_pool_007:07'][0];state=env.reset(seed);traces.append([]);step(kind('source'))
 actual=[env.actions[i] for i in state['legal'] if env.actions[i]['Kind']=='skillSideways' and env.actions[i]['Card']=='hero_skill_pool_007:07']
 assert actual and all(a['Option']==0 for a in actual) and state['state']['sourceUsedThisTurn']
 reports=[audit(t) for t in traces];assert not any(r['errors'] for r in reports),reports
 result={'scope':env.info['scope'],'fixtures':fixtures,'actual_attack_consumers':consumers,'reports':reports,'trace':traces,'original_skill_text_sha256':hashlib.sha256(Path('resources/text_json/skill.json').read_bytes()).hexdigest(),'negative_checks':['wrong_printed_block_value','native_once_per_turn','source_claim_after_repel_promise','repel_promise_after_actual_source_claim'],'limits':'Directed formal native uses in twelve skill/use branches; not PPO usage, all card types/colors, source payment alternatives, whole-turn subsequent consumers, or Unity acceptance.'}
 Path('AutomationOutputs/BattleRl/20261004_native_sideways_skills.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
 print(json.dumps({'scope':env.info['scope'],'fixtures':fixtures,'audit_checks':sum(r['checks'] for r in reports)}))
except Exception as error:
 Path(f'AutomationOutputs/BattleRl/20261004_native_sideways_failure_{time.time_ns()}.json').write_text(json.dumps({'error':str(error),'trace':traces},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
