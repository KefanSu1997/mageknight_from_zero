import json,collections,statistics
from pathlib import Path
r=Path('H:/MageKnight_RL_Runs/autodl_20261013/diagnostic_20261014')
b=json.loads((r/'blue_window.json').read_text())['rows'];g=json.loads((r/'green_window.json').read_text())['rows'];out=[]
for lo,hi in [(30,49),(50,69),(70,89)]:
 groups=[]
 for rows in [b,g]:
  t=collections.defaultdict(list)
  for x in rows:
   if lo<=x['update']<=hi:t[x['task']].append(x['won'])
  groups.append(t)
 common=groups[0].keys() & groups[1].keys()
 out.append(dict(range=[lo,hi],shared_tasks=len(common),blue=statistics.mean(statistics.mean(groups[0][k]) for k in common),green=statistics.mean(statistics.mean(groups[1][k]) for k in common),scope='shared task IDs equal-weight visits; not paired seeds or identical live states'))
print(json.dumps(out));(r/'shared_task_comparison.json').write_text(json.dumps(out,indent=2))
