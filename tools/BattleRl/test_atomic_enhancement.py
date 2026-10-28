import json,time
from pathlib import Path
from train_sb3_atomic import configure_rules
from train_ppo import Rules
from trajectory_audit import audit
configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_build/BattleRl.dll')
r=Rules()
try:
 rows=json.load(open('H:/MageKnight_RL_Runs/atomic_dataset/train.json'))['tasks']
 rows=[r for r in rows if 'hero_skill_pool_003:09' in r['task']['definition'].get('Build',{}).get('SkillIds',[])]
 print('candidate tasks',len(rows),flush=True)
 for j,row in enumerate(rows[:64]):
  s=r.reset_task(row['task']['definition']);s=r.step(next(i for i in s['legal'] if r.actions[i]['Kind']=='fight'))
  choices=r.call(op='atomicChoices')['choices']
  selected=next((i for i,p in enumerate(choices) if any(a['Kind']=='sharedArm' for a in p)),None)
  if selected is not None:
   result=r.call(op='atomicStep',choice=selected);report=audit(result['micro'])
   doc=dict(task_id=row['task_id'],definition=row['task']['definition'],sequence=choices[selected],result=result,report=report)
   Path('H:/MageKnight_RL_Runs/atomic_enhancement_gate.json').write_text(json.dumps(doc,indent=2))
   print('found',j,choices[selected],report,flush=True);assert not report['errors'];break
 else:raise AssertionError('No positive enhancement witness in inspected tasks')
finally:r.close()
