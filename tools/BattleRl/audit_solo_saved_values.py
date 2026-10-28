"""Reconcile pre-update EV from stored behavior predictions, no model forward."""
import argparse,hashlib,json,math,statistics
from pathlib import Path
from solo_trajectory_audit import audit_native_journal

p=argparse.ArgumentParser();p.add_argument('--root',required=True);p.add_argument('--out',required=True);a=p.parse_args()
targets=[];values=[];episodes=[]
for path in sorted(Path(a.root).glob('episode_*/result.json')):
    result=json.loads(path.read_text())
    if not result.get('eligible_complete_episode'):raise ValueError('Not a complete training episode')
    raw=path.with_name('native.json').read_bytes()
    if hashlib.sha256(raw).hexdigest()!=result['archive']['sha256']:raise ValueError('Native SHA mismatch')
    records=json.loads(raw)['native']['records'];audit=audit_native_journal(records)
    if not audit['passed']:raise ValueError(str(audit['errors']))
    returns=[];remaining=0.
    for row in reversed(records):remaining+=row['Transition']['Reward'];returns.append(remaining)
    returns.reverse();predictions=[r['ValuePrediction'] for r in records]
    if not all(math.isfinite(v) for v in returns+predictions):raise ValueError('Nonfinite targets/predictions')
    targets.extend(returns);values.extend(predictions)
    episodes.append(dict(path=str(path),sha256=result['archive']['sha256'],decisions=len(records),
        return_scaled=remaining,fame=records[-1]['Transition']['Score']['Inventory']['Fame']))
if len(episodes)!=128:raise ValueError('Require all 128 complete episodes, found '+str(len(episodes)))
residuals=[y-v for y,v in zip(targets,values)];variance=statistics.pvariance(targets)
report=dict(episodes=128,samples=len(targets),gamma=1,gae_lambda=1,terminal_bootstrap=0,
    pre_update_ev=1-statistics.pvariance(residuals)/variance if variance else None,
    mse=statistics.mean(x*x for x in residuals),prediction_bias=-statistics.mean(residuals),
    target_variance=variance,target_mean=statistics.mean(targets),prediction_mean=statistics.mean(values),
    mean_episode_return=statistics.mean(e['return_scaled'] for e in episodes),
    scope='Stored behavior values and actual complete-scenario Monte Carlo returns; pre-update, not post-update or policy quality',
    evidence=episodes)
Path(a.out).write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k!='evidence'}))
