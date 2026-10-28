"""Read completed episode evidence only; never infer optimality from unused power."""
import argparse, collections, hashlib, json, statistics
from pathlib import Path

p=argparse.ArgumentParser();p.add_argument('--root',required=True);p.add_argument('--out',required=True);p.add_argument('--window-end',type=int);p.add_argument('--window-size',type=int,default=5);a=p.parse_args()
assert a.window_size>0
root=Path(a.root); batches=[json.loads(x) for x in (root/'episode_batches.jsonl').read_text().splitlines()]
report={'scope':'Archived training trajectories, separate visited states. Power present before advance is not proof of waste or legal useful attack. Ranged attacks are phase-filtered; disappearance is not evidence of destruction.', 'windows':[]}
for low,high in ([(a.window_end-a.window_size+1,a.window_end)] if a.window_end is not None else [(26,30),(36,40)]):
 records=collections.defaultdict(list); files=[]; wins=0
 selected=[b for b in batches if low<=b['update']<=high];assert len(selected)==high-low+1
 for b in selected:
  assert b['accepted_episodes']==128
  for e in b['episodes']:
   path=root/'episodes'/f"worker_{e['worker']}_episode_{e['episode']}.json"
   raw=path.read_bytes();d=json.loads(raw);assert d['certificate']['task_id']==e['task_id']
   files.append({'path':str(path),'sha256':hashlib.sha256(raw).hexdigest()});wins+=int(d['won'])
   for index,t in enumerate(d['trace']):
    before,after=t['before'],t['after'];phase=before['phase']
    if t['action']['Kind']!='advance' or before['phase']==after['phase']:continue
    attacks=before['attacks']; blocks=before['blocks']
    row={'episode':str(path),'trace_index':index,'won':bool(d['won']),
      'attack_power':sum(x['effectiveValue'] for x in attacks),
      'block_power':sum(x['value'] for x in blocks),
      'hand_nonwound':sum(x!='wound' for x in before['hand']),
      'hand_count':len(before['hand']), 'ready_units':sum(x['ready'] and not x['destroyed'] for x in before['units']),
      'remaining_enemies':sum(not x['defeated'] for x in before['enemies'])}
    # Card IDs for wounds vary; do not expose an unverified nonwound count.
    del row['hand_nonwound']
    records[phase].append(row)
 assert len(files)==128*(high-low+1)
 summary={}
 for phase,rows in records.items():
  summary[phase]={'boundaries':len(rows)}
  for key in ['attack_power','block_power','hand_count','ready_units','remaining_enemies']:
   values=[r[key] for r in rows]
   summary[phase][key]={'mean':statistics.mean(values),'positive':sum(v>0 for v in values)}
 report['windows'].append({'updates':[low,high],'episodes':len(files),'wins':wins,'summary':summary,'boundaries':dict(records),'files':files})
Path(a.out).write_text(json.dumps(report));print(json.dumps([{k:v for k,v in w.items() if k not in ('files','boundaries')} for w in report['windows']]))
