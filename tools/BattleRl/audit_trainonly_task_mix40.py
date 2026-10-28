"""Describe task visitation shifts; no environment or policy changes."""
import json,collections,statistics,hashlib
from pathlib import Path
root=Path('H:/MageKnight_RL_Runs/autodl_20261013/diagnostic_20261014')
source=Path('H:/MageKnight_RL_Runs/atomic_dataset/train.json');dataset=json.loads(source.read_text())
tasks={r['task_id']:r['task']['definition'] for r in dataset['tasks']+dataset.get('unknown_tasks',[])}
rows=[json.loads(s) for s in (root/'trainonly_performance_snapshot_0040.jsonl').read_text().splitlines()]
windows=[[r for r in rows if low<=r['update']<high] for low,high in [(20,30),(30,40)]]
assert all(len(w)==1280 for w in windows)
fields={'hero':lambda d:d['HeroId'],'family':lambda d:d.get('Build',{}).get('Family','not_provided'),'enemy_count':lambda d:str(len(d['EnemyGroups'][0])),'unit_count':lambda d:str(len(d['Build']['UnitIds'])) if 'UnitIds' in d.get('Build',{}) else 'not_provided','initial_wounds':lambda d:str(d.get('Build',{}).get('InitialWounds','not_provided'))}
report={'scope':'Training visitation descriptive only. Shared-task subset is selected and stochastic; no causal attribution or fixed evaluation.','train_sha':hashlib.sha256(source.read_bytes()).hexdigest(),'updates':[[21,30],[31,40]],'factors':{}}
for field,key in fields.items():
 counters=[collections.Counter(key(tasks[r['task_id']]) for r in w) for w in windows]
 levels=sorted(set(counters[0])|set(counters[1]))
 report['factors'][field]={'counts':[dict(c) for c in counters],'total_variation':sum(abs(counters[0][x]-counters[1][x]) for x in levels)/2560}
grouped=[]
for w in windows:
 g=collections.defaultdict(list)
 for r in w:g[r['task_id']].append(r)
 grouped.append(g)
common=set(grouped[0])&set(grouped[1]);assert common
report['shared_tasks']={'count':len(common),'episodes':[sum(len(g[t]) for t in common) for g in grouped],
 'equal_task_mean_reward':[statistics.mean(statistics.mean(r['training_return'] for r in g[t]) for t in common) for g in grouped],
 'equal_task_win_rate':[statistics.mean(statistics.mean(float(r['won']) for r in g[t]) for t in common) for g in grouped]}
(root/'trainonly_task_mix40.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
