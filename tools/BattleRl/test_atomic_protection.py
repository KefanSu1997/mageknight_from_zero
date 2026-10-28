import json,time
from pathlib import Path
from train_sb3_atomic import configure_rules
from train_ppo import Rules
from trajectory_audit import audit
configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_build/BattleRl.dll')
r=Rules()
try:
 rows=json.load(open('H:/MageKnight_RL_Runs/atomic_dataset/train.json'))['tasks']
 rows=[r for r in rows if 'items_002' in r['task']['definition'].get('Build',{}).get('AdditionalCards',[])]
 print('candidate tasks',len(rows),flush=True)
 for j,row in enumerate(rows[:64]):
  s=r.reset_task(row['task']['definition']);s=r.step(next(i for i in s['legal'] if r.actions[i]['Kind']=='fight'))
  choices=r.call(op='atomicChoices')['choices']
  selected=next((i for i,p in enumerate(choices) if p[-1]['Kind']=='artifact' and p[-1]['Card']=='items_002' and p[-1]['Choice']=='burn'),None)
  if selected is not None:
   result=r.call(op='atomicStep',choice=selected);trace=result['micro']
   while result['state']['phase']!='End':
    choices=r.call(op='atomicChoices')['choices'];selected=next(i for i,p in enumerate(choices) if p[-1]['Kind']=='advance')
    result=r.call(op='atomicStep',choice=selected);trace+=result['micro']
   choices=r.call(op='atomicChoices')['choices'];selected=next(i for i,p in enumerate(choices) if any(a['Kind']=='protectionCleanup' and a['Option']==1 for a in p))
   result=r.call(op='atomicStep',choice=selected);result['micro'][-1]['task_boundary']='formal_single_battle_v1';trace+=result['micro'];report=audit(trace)
   doc=dict(task_id=row['task_id'],definition=row['task']['definition'],sequence=choices[selected],result=result,report=report)
   Path('H:/MageKnight_RL_Runs/atomic_protection_gate.json').write_text(json.dumps(doc,indent=2))
   print('found',j,choices[selected],report,flush=True);assert not report['errors'];break
 else:raise AssertionError('No positive enhancement witness in inspected tasks')
finally:r.close()
