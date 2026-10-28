import json,sys,time
from pathlib import Path
from train_sb3_atomic import configure_rules
from train_ppo import Rules
from trajectory_audit import audit
configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_build/BattleRl.dll')
fixtures=json.load(open('H:/MageKnight_RL_Runs/atomic_fixtures.json'));fixtures.update(json.load(open('H:/MageKnight_RL_Runs/atomic_positive_fixtures.json')));results=[]
for kind,f in fixtures.items():
 if kind in ('protectionCleanup','sharedArm','defend'):continue
 d=json.load(open(f['path'],encoding='utf-8'));r=Rules();keys={a['Key']:i for i,a in enumerate(r.actions)}
 try:
  current=r.reset_task(d['certificate']['task']['definition'])
  current=r.step(keys[d['formal_entry']['action']['Key']]);trace=[]
  for original in d['trace'][:f['step']]:
   before=current;current=r.step(keys[original['action']['Key']])
   trace.append(dict(before=before['state'],after=current['state'],action=original['action']))
  if f.get('clear'):
   index=next(i for i in current['legal'] if r.actions[i]['Kind']==f['clear'])
   before=current;current=r.step(index)
   trace.append(dict(before=before['state'],after=current['state'],action=r.actions[index]))
  for _ in range(6):
   start=time.time();choices=r.call(op='atomicChoices')['choices']
   matches=[(i,p) for i,p in enumerate(choices) if any(a['Kind']==kind for a in p)]
   if matches:break
   if kind in ('sharedArm','defend') and not f.get('clear'):break
   if kind!='sharedSourceReturn':raise AssertionError((kind,'missing atomic consumer'))
   a=next(i for i,p in enumerate(choices) if p[-1]['Kind']=='advance')
   current=r.call(op='atomicStep',choice=a);trace.extend(current['micro'])
  if kind in ('sharedArm','defend') and not matches and not f.get('clear'):
   results.append(dict(kind=kind,scope='preselection state without executable consumer exposes no standalone selection',support=len(choices)))
   print(results[-1],flush=True);continue
  assert matches,kind
  a,sequence=matches[0]
  result=r.call(op='atomicStep',choice=a)
  assert not result['atomic_error'],result
  micro=result['micro']
  if result['done']:micro[-1]['task_boundary']='formal_single_battle_v1'
  report=audit(trace+micro)
  record=dict(kind=kind,sequence=sequence,errors=report['errors'],checks=report['checks'],support=len(choices))
  results.append(record);print(json.dumps(record),flush=True)
  assert not report['errors'],record
 finally:r.close()
Path('H:/MageKnight_RL_Runs/atomic_consumer_gate.json').write_text(json.dumps(results,indent=2))
