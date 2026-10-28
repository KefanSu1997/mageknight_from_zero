"""Read-only compact snapshot; process identity and progress, never auto-restart."""
import json
import time
from pathlib import Path

ROOT = Path('/root/mageknight_solo_conquest_A_forest100_20261022')

def read(path):
    try:
        return json.loads(path.read_text())
    except (FileNotFoundError, json.JSONDecodeError):
        return None

status = read(Path('/root/solo_conquest_launch_status.json')) or {}
result = dict(time=time.time(), launch=status, processes={}, progress={})
for role in ('trainer', 'evaluator'):
    pid = status.get(role+'_pid')
    proc = Path('/proc')/str(pid)
    if proc.exists():
        result['processes'][role] = dict(pid=pid,
            command=(proc/'cmdline').read_bytes().replace(b'\0', b' ').decode(),
            stat=(proc/'stat').read_text())
    else:
        result['processes'][role] = dict(pid=pid, present=False)
for path in ROOT.glob('**/progress.json'):
    value = read(path)
    if value is None:
        continue
    active = value.pop('active', [])
    decisions = [row.get('decisions', 0) for row in active]
    value.update(active_workers=len(active), decisions_min=min(decisions, default=0),
                 decisions_max=max(decisions, default=0), decisions_sum=sum(decisions))
    result['progress'][str(path.relative_to(ROOT))] = value
for name in ('phase.json', 'review_required.json', 'training_complete.json'):
    result[name] = read(ROOT/name)
metrics = read(ROOT/'metrics.json') or []
result['metrics_count'] = len(metrics)
result['latest_metrics'] = metrics[-1] if metrics else None
episode_reports = []
for path in ROOT.glob('**/episode_*/result.json'):
    row = read(path)
    if row is None:
        continue
    audit = row.get('audit', {})
    timings = row.get('timings', [])
    policy = [t for t in timings if t.get('operation') == 'policy']
    episode_reports.append(dict(path=str(path.relative_to(ROOT)),
        status=row.get('status'), error=row.get('error'),
        decisions=row.get('decisions'), wall_seconds=row.get('wall_seconds'),
        return_scaled=row.get('return_scaled'), audit_passed=audit.get('passed'),
        audit_errors=audit.get('errors', []),
        positive_cycle_candidates=[w for w in audit.get('warnings', []) if w.get('cycle_return', 0)>1e-8],
        prepare_seconds=sum(t.get('prepare_seconds', 0) for t in policy),
        inference_seconds=sum(t.get('inference_seconds', 0) for t in policy)))
result['completed_episode_files'] = len(episode_reports)
result['episode_status_counts'] = {s:sum(r['status']==s for r in episode_reports)
                                  for s in sorted(set(r['status'] for r in episode_reports))}
result['episode_alerts'] = [r for r in episode_reports if r['status']!='complete'
                            or r['audit_errors'] or r['positive_cycle_candidates']]
result['first_completed_samples'] = episode_reports[:7]
print(json.dumps(result))
