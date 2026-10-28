"""Compare recorded training windows only; never infer fixed-evaluation gains."""
import json, math
from pathlib import Path
root=Path('H:/MageKnight_RL_Runs/autodl_20261013')
runs=['remote_repaired_trainonly300','entity_baseline','baseline']
def read(name):
 p=root/name/'metrics.jsonl'
 return [json.loads(s) for s in p.read_text(encoding='utf-8').splitlines()] if p.exists() else []
allrows={name:read(name) for name in runs}; current=allrows[runs[0]]
if not current:raise SystemExit('waiting for synced training metrics')
end=max(r['update'] for r in current);start=max(1,end-4)
report={'window':[start,end],'scope':'Same update indices, different architectures and adaptive task visitation. Training diagnostics only; no fixed evaluation or statistical significance claim. Recent100 reward windows are not all-game means.','runs':{}}
for name,rows in allrows.items():
 selected=[r for r in rows if start<=r['update']<=end]
 def mean(values):
  values=[v for v in values if v is not None and math.isfinite(v)]
  return sum(values)/len(values) if values else None
 report['runs'][name]={'points':len(selected),'mean_recent100_reward':mean([r['recent_mean_reward'] for r in selected]),'mean_ev_before':mean([r['value_before']['ev'] for r in selected]),'mean_ev_after':mean([r['value_after']['ev'] for r in selected]),'rows':[{'update':r['update'],'reward':r['recent_mean_reward'],'ev_before':r['value_before']['ev'],'ev_after':r['value_after']['ev'],'mse_after':r['value_after']['mse']} for r in selected]}
out=root/'diagnostic_20261014/trainonly_window_comparison.json';out.write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report))
