import json,copy
from pathlib import Path
from train_sb3_atomic import configure_rules
from train_ppo import Rules
from trajectory_audit import audit
configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_build/BattleRl.dll')
f=json.load(open('H:/MageKnight_RL_Runs/atomic_positive_fixtures.json'))['defend'];d=json.load(open(f['path'],encoding='utf-8'))
task=copy.deepcopy(d['certificate']['task']['definition']);task['EnemyGroups'][0][0]='monster_057'
r=Rules()
try:
 keys={a['Key']:i for i,a in enumerate(r.actions)}
 def setup():
  s=r.reset_task(task);s=r.step(next(i for i in s['legal'] if r.actions[i]['Kind']=='fight'));trace=[]
  for i in (0,1,3,5,7):
   a=d['trace'][i]['action'];before=s;s=r.step(keys[a['Key']]);trace.append(dict(before=before['state'],after=s['state'],action=a))
  return trace
 trace=setup();choices=r.call(op='atomicChoices')['choices']
 matched=[(i,p) for i,p in enumerate(choices) if p[-1]['Kind']=='attack' and p[-1]['Targets']==1 and p[-1]['Parts']==3 and any(a['Kind']=='defend' for a in p)]
 sources={a['Parts'] for _,p in matched for a in p if a['Kind']=='defend'}
 assert sources=={0,1},sources
 reports=[]
 for _,sequence in matched:
  trace=setup();choices=r.call(op='atomicChoices')['choices'];i=choices.index(sequence)
  result=r.call(op='atomicStep',choice=i);report=audit(trace+result['micro'])
  assert not result['atomic_error'] and not report['errors'],report
  reports.append(dict(sequence=sequence,checks=report['checks']))
 Path('H:/MageKnight_RL_Runs/atomic_defense_support_gate.json').write_text(json.dumps(dict(sources=sorted(sources),reports=reports,scope='both original defender sources retained for one attacked target; controlled formal fixture'),indent=2))
 print('PASS both sources',sources,flush=True)
finally:r.close()
