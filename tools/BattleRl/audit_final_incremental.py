"""Read-only incremental evidence audit for completed ready-collector updates."""
import argparse, hashlib, json, statistics
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--root',required=True);a=p.parse_args();root=Path(a.root)
def lines(name):
    return [json.loads(x) for x in (root/name).read_text().splitlines() if x]
metrics={r['update']:r for r in lines('metrics.jsonl')}
batches={r['update']:r for r in lines('episode_batches.jsonl')}
last=max(set(metrics)&set(batches))
folder=root/'monitor_audits';folder.mkdir(exist_ok=True)
cursor_path=folder/'cursor.json'
cursor=json.loads(cursor_path.read_text())['completed'] if cursor_path.exists() else min(batches)-1
report_path=folder/f'completed_{last:04d}.json'
if report_path.exists():
    print(json.dumps({'existing_report':str(report_path),'completed':last}));raise SystemExit(0)
report={'completed':last,'previous_cursor':cursor,'files':[],'batches':[],
        'scope':'Complete new batch file identities, trace lengths and wins; timing and last5/10 training windows. Does not certify all rules/payment/support/text/irreversible paths or reward hacking; no fixed evaluation/causal claim.'}
for update in sorted(u for u in batches if cursor<u<=last):
    b=batches[update];m=metrics[update]
    assert b['accepted_episodes']==128 and len(b['episodes'])==128
    count=steps=wins=0
    for e,length in zip(b['episodes'],b['lengths']):
        path=root/'episodes'/f"worker_{e['worker']}_episode_{e['episode']}.json"
        raw=path.read_bytes();d=json.loads(raw)
        assert d['certificate']['task_id']==e['task_id']
        assert len(d['trace'])==length
        assert isinstance(d['won'],bool)
        count+=1;steps+=len(d['trace']);wins+=int(d['won'])
        report['files'].append({'path':str(path),'sha256':hashlib.sha256(raw).hexdigest()})
    assert steps==b['transitions']
    if update-1 in metrics:
        assert count==m['episodes']-metrics[update-1]['episodes']
        assert wins==m['wins']-metrics[update-1]['wins']
    report['batches'].append({'update':update,'episodes':count,'operations':steps,'wins':wins})
report['windows']=[]
for size in (5,10):
    lo=last-size+1
    if lo<1 or any(u not in metrics for u in range(lo,last+1)):continue
    rows=[metrics[u] for u in range(lo,last+1)];prior=metrics.get(lo-1,{'episodes':0,'wins':0,'timesteps':0})
    episodes=rows[-1]['episodes']-prior['episodes'];operations=rows[-1]['timesteps']-prior['timesteps']
    def mean(field):return statistics.mean(float(r[field]) for r in rows)
    window={'updates':[lo,last],'episodes':episodes,'operations':operations,
            'wins':rows[-1]['wins']-prior['wins'],
            'recent100_reward_mean':mean('recent_mean_reward'),
            'EV_before_mean':statistics.mean(r['value_before']['ev'] for r in rows),
            'EV_after_mean':statistics.mean(r['value_after']['ev'] for r in rows),
            'MSE_before_mean':statistics.mean(r['value_before']['mse'] for r in rows),
            'MSE_after_mean':statistics.mean(r['value_after']['mse'] for r in rows),
            'bias_after_mean':statistics.mean(r['value_after']['bias'] for r in rows),
            'audit_failures':sum(r['audit_failed_episodes'] for r in rows)}
    for field in ('collection_seconds','optimization_seconds','archive_seconds'):
        total=sum(r[field] for r in rows);window[field]=total/size
        window[field+'_per_episode']=total/episodes;window[field+'_per_operation']=total/operations
    for key in ('train/approx_kl','train/clip_fraction','train/entropy_loss'):
        window[key]=statistics.mean(r['logger'][key] for r in rows)
    report['windows'].append(window)
report_path.write_text(json.dumps(report,indent=2))
cursor_path.write_text(json.dumps({'completed':last,'report':str(report_path),
                                  'report_sha256':hashlib.sha256(report_path.read_bytes()).hexdigest()}))
print(json.dumps({k:v for k,v in report.items() if k!='files'}))
