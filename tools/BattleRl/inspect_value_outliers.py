import argparse,json,re
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--root',required=True);p.add_argument('--report',required=True);p.add_argument('--out',required=True);a=p.parse_args();root=Path(a.root)
r=json.loads(Path(a.report).read_text());wanted={x['context']:x for x in r['phase_value_fit']['Block/enemies_remaining']['largest_errors']};docs={}
with (root/'contexts/contexts.jsonl').open() as f:
 for ctx,line in enumerate(f,1):
  if ctx in wanted:docs[ctx]=json.loads(line)
by_support={int(d['support_id']):ctx for ctx,d in docs.items()};rows=[]
for line in (root/'episode_batches.jsonl').read_text().splitlines():
 b=json.loads(line)
 if not 46<=b['update']<=50:continue
 for e in b['episodes']:
  path=root/'episodes'/f"worker_{e['worker']}_episode_{e['episode']}.json";d=json.loads(path.read_text())
  for index,t in enumerate(d['trace']):
   if int(t['support_id']) not in by_support:continue
   ctx=by_support[int(t['support_id'])];doc=docs[ctx];state=t['before'];features={int(k):float(v) for k,v in re.findall(r'state\.(\d+)=([-+\d.eE]+)',doc['state'])}
   enemies=[]
   for slot,enemy in enumerate(state['enemies']):
    coord=487+110*slot
    # Checked against the public coordinate schema, not hidden state.
    enemies.append(dict(slot=slot,id=enemy['id'],defeated=enemy['defeated'],feature_present=features.get(coord,0),feature_defeated=features.get(coord+1,0)))
   rows.append(dict(**wanted[ctx],episode=str(path),trace_index=index,phase=state['phase'],won=d['won'],enemies=enemies,hand=state['hand'],action=t['action'],root_kinds=sorted({seq[-1]['Kind'] for seq in doc['operations']}),candidate_count=len(doc['operations'])))
assert len(rows)==len(wanted)
report=dict(scope='Exact saved context/support/episode joins for ten largest Block residuals. Public enemy coordinates only; no replay or causal claim.',rows=rows)
Path(a.out).write_text(json.dumps(report,indent=2));print(json.dumps(report))
