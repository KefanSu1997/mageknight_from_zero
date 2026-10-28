"""Complete training episode windows, reconciled to recorded updates."""
import argparse,json,hashlib,statistics
from pathlib import Path
root=Path('H:/MageKnight_RL_Runs/autodl_20261013')
out=root/'diagnostic_20261014'
parser=argparse.ArgumentParser();parser.add_argument('--snapshot-suffix',default='');args=parser.parse_args()
assert not any(c in args.snapshot_suffix for c in '/\\')
p=out/f'trainonly_performance_snapshot{args.snapshot_suffix}.jsonl';m=out/f'trainonly_metrics_snapshot{args.snapshot_suffix}.jsonl'
episodes=[json.loads(s) for s in p.read_text().splitlines() if s]
metrics={r['update']:r for r in (json.loads(s) for s in m.read_text().splitlines() if s)}
report={'scope':'Complete training episodes, not fixed evaluation. Adaptive visitation and state distributions change; no causal or significance claim. Performance update is zero-based, metrics update one-based.',
        'source_sha':{q.name:hashlib.sha256(q.read_bytes()).hexdigest() for q in (p,m)},'windows':[]}
for hi in range(10,max(metrics)+1,10):
 lo=hi-9
 if hi not in metrics:continue
 selected=[r for r in episodes if lo-1<=r['update']<hi]
 before=metrics.get(lo-1,{'episodes':0,'wins':0})
 assert len(selected)==metrics[hi]['episodes']-before['episodes']==128*(hi-lo+1)
 assert sum(bool(r['won']) for r in selected)==metrics[hi]['wins']-before['wins']
 assert all(sum(r['update']==i for r in selected)==128 for i in range(lo-1,hi))
 x={'updates':[lo,hi],'episodes':len(selected),'wins':sum(bool(r['won']) for r in selected),'unique_tasks':len({r['task_id'] for r in selected}),
    'reward':statistics.mean(r['training_return'] for r in selected),'kills':statistics.mean(r['kills'] for r in selected),
    'new_wound_cards':statistics.mean(r['new_wound_cards'] for r in selected),'steps':statistics.mean(r['steps'] for r in selected),
    'timeouts':sum(bool(r['timeout']) for r in selected),
    'EV_before_mean':statistics.mean(metrics[i]['value_before']['ev'] for i in range(lo,hi+1)),
    'EV_after_mean':statistics.mean(metrics[i]['value_after']['ev'] for i in range(lo,hi+1)),'historical_training_wins':{}}
 for name in ['entity_baseline','baseline']:
  old={r['update']:r for r in (json.loads(s) for s in (root/name/'metrics.jsonl').read_text().splitlines())}
  if hi not in old or (lo>1 and lo-1 not in old):continue
  prior=old.get(lo-1,{'episodes':0,'wins':0})
  x['historical_training_wins'][name]={'wins':old[hi]['wins']-prior['wins'],'episodes':old[hi]['episodes']-prior['episodes']}
 report['windows'].append(x)
(out/f'trainonly_complete_windows{args.snapshot_suffix}.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
