import json
from pathlib import Path
r=Path('/root/autodl-tmp/mageknight/remote_final_semantic_fast300');b=[json.loads(x) for x in (r/'episode_batches.jsonl').read_text().splitlines() if json.loads(x)['update']==80][0]
for e in b['episodes']:
 if e['task_id'].startswith('5b2a2c'):
  p=r/'episodes'/f"worker_{e['worker']}_episode_{e['episode']}.json"
  Path('/root/autodl-tmp/mageknight/optimization_stage_20261014/blue_failure_example.json').write_bytes(p.read_bytes())
