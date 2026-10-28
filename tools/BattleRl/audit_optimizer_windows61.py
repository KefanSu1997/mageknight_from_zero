"""Compare recorded optimizer diagnostics, not matched-task policy quality."""
import json,hashlib,statistics
from pathlib import Path
root=Path('H:/MageKnight_RL_Runs/autodl_20261013');report={}
fields=['train/approx_kl','train/clip_fraction','train/entropy_loss','train/policy_gradient_loss','train/value_loss']
for name in ['remote_repaired_trainonly300','entity_baseline','baseline']:
 p=root/name/'metrics.jsonl';raw=p.read_bytes();rows=[json.loads(x) for x in raw.splitlines()];rows=[r for r in rows if r['update']<=60]
 assert [r['update'] for r in rows]==list(range(1,61))
 windows=[];previous=0
 for start in range(0,60,10):
  segment=rows[start:start+10];deltas=[]
  for r in segment:
   value=r['logger']['train/n_updates'];deltas.append(value-previous);previous=value
  windows.append(dict(first=start+1,last=start+10,epochs_logged=deltas,statistics={k:dict(mean=statistics.mean(r['logger'][k] for r in segment),min=min(r['logger'][k] for r in segment),max=max(r['logger'][k] for r in segment)) for k in fields}))
 report[name]=dict(source=str(p),sha256=hashlib.sha256(raw).hexdigest(),windows=windows)
out=root/'diagnostic_20261014/optimizer_windows61.json';assert not out.exists()
out.write_text(json.dumps(dict(runs=report,scope='Exact logged first60 update windows. Entropy estimator/action parameterization and adaptive task streams differ; absolute entropy and optimizer losses are not direct capability comparisons. Logged epoch count cannot exclude early exit inside the final epoch; no such claim.'),indent=2))
for name,r in report.items():
 print(name)
 for w in r['windows']:print(w['first'],w['last'],{k:round(v['mean'],6) for k,v in w['statistics'].items()},w['epochs_logged'])
