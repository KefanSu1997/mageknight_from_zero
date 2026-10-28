"""Actual native Forked Lightning, separate enemy instances and real consumption."""
import os,json,time,hashlib
from pathlib import Path
from copy import deepcopy
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
from train_ppo import Rules
from trajectory_audit import audit,classify_attack_sources
env=Rules();skill='hero_skill_pool_001:05';traces=[];fixtures=[]
unit_text={c['id']:c for name in ('army','army_2') for c in json.loads(Path('resources/text_json/'+name+'.json').read_text(encoding='utf-8'))}
def step(i):
 global state
 n=env.step(i);traces[-1].append({'before':state['state'],'action':env.actions[i],'after':n['state'],'reward':n['reward']});state=n
def choose(kind):return next(i for i in state['legal'] if env.actions[i]['Kind']==kind)
def start(seed,phase):
 global state
 state=env.reset(seed);traces.append([]);step(choose('fight'))
 while state['state']['phase']!=phase:step(choose('advance'))
def fork(count):return next(i for i in state['legal'] if env.actions[i]['Kind']=='skillFork' and env.actions[i]['Targets']==(1<<count)-1)
try:
 seeds=[]
 for seed in range(6,1800,7):
  b=env.call(op='inspectInventory',seed=seed)['state']
  if skill in b['skills'] and len(b['enemies'])==3:seeds.append(seed)
  if len(seeds)>=15:break
 assert seeds
 for phase in ('Ranged','Melee'):
  for count in (1,2,3):
   seed=seeds[0];start(seed,phase);step(fork(count));assert not any(env.actions[i]['Kind']=='skillFork' for i in state['legal'])
   assert all(c['requiredEnemy'] in {e['instance'] for e in state['state']['enemies'][:count]} for c in state['state']['attacks'] if c['skill']==skill)
   fixtures.append({'seed':seed,'phase':phase,'selected_count':count})
 # Independent oracle rejects merging the separate target-bound contributions.
 bad=deepcopy(traces[2]);t=bad[-1];t['after']['attacks'][-1]['requiredEnemy']=t['after']['attacks'][0]['requiredEnemy'];assert audit(bad)['errors']
 consumed=False
 for seed in seeds:
  start(seed,'Ranged');step(fork(1))
  # Select a genuinely legal original ranged/siege unit, not a new training card.
  ranged=[]
  for i in state['legal']:
   a=env.actions[i]
   if a['Kind']!='unit' or a['Input']:continue
   text=unit_text[a['Card']]['abilities'][int(a['Choice'])];text=text if isinstance(text,str) else text.get('effect','')
   if '远程' in text or '攻城' in text:ranged.append(i)
  if not ranged:traces.pop();continue
  step(ranged[0]);part=next(j for j,c in enumerate(state['state']['attacks']) if c['skill']==skill)
  correct=[i for i in state['legal'] if env.actions[i]['Kind']=='attack' and env.actions[i]['Targets']==1 and env.actions[i]['Parts']&(1<<part)]
  if not correct:continue
  assert not any(env.actions[i]['Kind']=='attack' and env.actions[i]['Targets']==2 and env.actions[i]['Parts']&(1<<part) for i in state['legal']),('bound_part_cannot_be_transferred',seed)
  step(correct[0]);assert state['state']['enemies'][0]['defeated'] and not any(c['skill']==skill and c['requiredEnemy']==state['state']['enemies'][0]['instance'] for c in state['state']['attacks'])
  fixtures.append({'seed':seed,'phase':'Ranged','actual_same_target_attack_consumer':True});consumed=True;break
 assert consumed,'Need actual target-bound contribution consumption'
 wrong=deepcopy(traces[-1]);wrong[-1]['action']['Targets']=2
 assert any(e['rule']=='selected target-bound contribution cannot be transferred to a different actual enemy instance' for e in audit(wrong)['errors'])
 reports=[audit(t) for t in traces];assert not any(r['errors'] for r in reports),reports
 consumers=[r for t in traces for r in classify_attack_sources(t) if r['mode'].startswith('skillFork:')];assert consumers
 result={'scope':env.info['scope'],'fixtures':fixtures,'reports':reports,'trace':traces,'actual_attack_consumers':consumers,'original_skill_text_sha256':hashlib.sha256(Path('resources/text_json/skill.json').read_bytes()).hexdigest(),'negative_checks':['merged_target_binding','once_per_turn','transfer_selected_part_to_different_enemy','wrong_target_in_saved_consumer'],'limits':'Native one/two/three original targets in two formal phases and one actual ranged consumer; not all enemy abilities, more than three enemies/eight contributions, borrowed skill, PPO coverage or Unity acceptance.'}
 Path('AutomationOutputs/BattleRl/20261004_native_fork.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps({'fixtures':fixtures,'audit_checks':sum(r['checks'] for r in reports),'actual_attack_consumers':consumers}))
except Exception as error:
 Path(f'AutomationOutputs/BattleRl/20261004_native_fork_failure_{time.time_ns()}.json').write_text(json.dumps({'error':str(error),'trace':traces},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
