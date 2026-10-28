import json
from pathlib import Path
from train_sb3_atomic import configure_rules
from train_ppo import Rules
from task_feasibility import digest,file_hash
configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_build/BattleRl.dll')
r=Rules();out=Path('H:/MageKnight_RL_Runs/atomic_dataset');out.mkdir(exist_ok=True)
try:
 cat=digest(r.actions);sha=file_hash('H:/MageKnight_RL_Runs/atomic_adapter_build/BattleRl.dll')
 for split in ('train','eval'):
  original=json.load(open(f'AutomationOutputs/BattleRl/20261011_fixed5000_dataset/{split}.json'))
  rows=original['tasks']+original.get('unknown_tasks',[])
  for row in rows:
   old=row['task_id'];oldsha=row['task']['rules_sha256']
   row['task']['rules_sha256']=sha;row['task']['catalog_sha256']=cat;row['task_id']=digest(row['task'])
   row['atomic_rebinding']={'parent_task_id':old,'parent_rules_sha256':oldsha,'scope':'same definition and initial state SHA; reset checks every actual visit; not new solvability certificate'}
  (out/f'{split}.json').write_text(json.dumps({'tasks':rows}),encoding='utf-8')
 print('4000 train / 1000 validation bindings saved',flush=True)
finally:r.close()
