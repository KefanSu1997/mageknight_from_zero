import json
from pathlib import Path
r=Path('/root/autodl-tmp/mageknight/remote_final_semantic_fast300');b=[json.loads(x) for x in (r/'episode_batches.jsonl').read_text().splitlines() if json.loads(x)['update']==80][0]
for e in b['episodes']:
 if not e['task_id'].startswith('5b2a2c'):continue
 p=r/'episodes'/f"worker_{e['worker']}_episode_{e['episode']}.json";d=json.loads(p.read_text());print(p)
 for t in d['trace']:
  s=t['before'];z=t['after'];print(json.dumps({'phase':s['phase'],'action':t['action'],'attack_before':s['attacks'],'attack_after':z['attacks'],'kills':z['kills'],'hand_before':s['hand'],'hand_after':z['hand']},ensure_ascii=False))
