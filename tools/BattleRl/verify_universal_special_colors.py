"""Use actual gold/day and black/night source tokens for native Universal Power."""
import os,json,time
from pathlib import Path
os.environ.setdefault('BATTLE_RL_PROTOCOL','--serve-journey-artifacts')
from train_ppo import Rules
from trajectory_audit import audit
env=Rules();traces=[];fixtures=[];skill='hero_skill_pool_005:07'
def step(i):
 global state
 n=env.step(i);traces[-1].append({'before':state['state'],'action':env.actions[i],'after':n['state'],'reward':n['reward']});state=n
try:
 seeds=[]
 for seed in range(2,1800,7):
  if skill in env.call(op='inspectInventory',seed=seed)['state']['skills']:seeds.append(seed)
  if len(seeds)>=25:break
 for color,option in [('Gold',4),('Black',5)]:
  for seed in seeds:
   state=env.reset(seed);traces.append([])
   if color=='Black':
    while state['state']['dayPart']=='Day' and not state['done']:
     step(next(i for i in state['legal'] if env.actions[i]['Kind']=='nextTurn'))
   source=next((i for i in state['legal'] if env.actions[i]['Kind']=='source' and env.actions[i]['Option']==option),None)
   if source is None:traces.pop();continue
   step(source)
   action=next((i for i in state['legal'] if env.actions[i]['Kind']=='skillSideways' and env.actions[i]['Card']==skill and env.actions[i]['Choice']=='Influence' and env.actions[i]['Option']==option),None)
   assert action is not None,('real_selected_color_missing',color,seed)
   before=state['state'];step(action);after=state['state']
   assert after['tokens'].get(color,0)==before['tokens'].get(color,0)-1 and after['influence']==before['influence']+3
   fixtures.append({'seed':seed,'color':color,'dayPart':before['dayPart'],'rounds':before['rounds'],'actual_source_taken_then_consumed':True});break
  else:raise AssertionError(('no_actual_source_fixture',color))
 reports=[audit(t) for t in traces];assert not any(r['errors'] for r in reports),reports
 result={'scope':env.info['scope'],'fixtures':fixtures,'reports':reports,'trace':traces,'limits':'Actual special-color token choices, including formal day/night transition; not every payment substitute or card class.'}
 Path('AutomationOutputs/BattleRl/20261004_universal_special_colors.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps({'fixtures':fixtures,'audit_checks':sum(r['checks'] for r in reports)}))
except Exception as error:
 Path(f'AutomationOutputs/BattleRl/20261004_universal_special_colors_failure_{time.time_ns()}.json').write_text(json.dumps({'error':str(error),'trace':traces},ensure_ascii=False,indent=2),encoding='utf-8');raise
finally:env.close()
