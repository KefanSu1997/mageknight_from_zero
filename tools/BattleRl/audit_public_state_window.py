"""Read completed on-policy updates only; no fixed evaluation or model changes."""
import json,hashlib,statistics
from pathlib import Path
root=Path('remote_public_state71')
def read(name):
 p=root/name;raw=p.read_bytes();return [json.loads(s) for s in raw.splitlines()],hashlib.sha256(raw).hexdigest()
metrics,msha=read('metrics.jsonl');perf,psha=read('performance.jsonl')
grad,gsha=read('gradient_groups.jsonl')
last=min(metrics[-1]['update'],grad[-1]['update']);assert 72<=last<=91
# Freeze only fully completed batches. The live performance journal can already
# contain the next rollout, which must not change evidence for this window.
metrics=[r for r in metrics if r['update']<=last]
perf=[r for r in perf if r['update']<last]
grad=[r for r in grad if r['update']<=last]
snapshot=root/f'window_evidence_{last:04d}'
snapshot.mkdir(exist_ok=True)
hashes=[]
for name,rows in [('metrics',metrics),('performance',perf),('gradients',grad)]:
 payload=(''.join(json.dumps(r,sort_keys=True)+'\n' for r in rows)).encode()
 path=snapshot/(name+'.jsonl')
 if path.exists():assert path.read_bytes()==payload,'Completed-window evidence changed'
 else:path.write_bytes(payload)
 hashes.append(hashlib.sha256(payload).hexdigest())
msha,psha,gsha=hashes
def window(start,end):
 rs=[r for r in perf if start-1<=r['update']<end and r.get('audit_valid',True)]
 ms=[r for r in metrics if start<=r['update']<=end]
 assert len(ms)==end-start+1 and len(rs)==128*len(ms)
 before=next(r for r in metrics if r['update']==start-1)
 after=next(r for r in metrics if r['update']==end)
 wins=sum(r['won'] for r in rs)
 assert after['episodes']-before['episodes']==len(rs) and after['wins']-before['wins']==wins
 return dict(start=start,end=end,episodes=len(rs),wins=wins,reward=statistics.mean(r['training_return'] for r in rs),ev_before=statistics.mean(r['value_before']['ev'] for r in ms),ev_after=statistics.mean(r['value_after']['ev'] for r in ms),collection_seconds=statistics.mean(r['collection_seconds'] for r in ms),optimization_seconds=statistics.mean(r['optimization_seconds'] for r in ms),kl=statistics.mean(r['logger']['train/approx_kl'] for r in ms))
new=window(72,last);old=window(62,71)
actual=[r for r in grad if 72<=r['update']<=last]
assert len(actual)==last-71 and all(r['minibatches'] for r in actual)
norms={k:[b[k] for r in actual for b in r['minibatches']] for k in ('shared','actor','critic')}
post=window(73,last) if last>=73 else None
reference=window(72-(last-72),71) if last>=73 else None
report=dict(completed=last,previous_ten=old,candidate=new,full_candidate_window=last==91,post_first_candidate_update=post,equal_length_prior_reference=reference,first_batch_reward_is_pre_change=True,gradient_calls=sum(len(r['minibatches']) for r in actual),preclip_norm_mean={k:statistics.mean(v) for k,v in norms.items()},source_sha=dict(metrics=msha,performance=psha,gradients=gsha),scope='Batch72 was collected before the first candidate optimizer update: its reward is not candidate-policy evidence. Batch73 onward uses modified updates. Actual training streams differ; not causal control or fixed evaluation.')
report['evidence_snapshot']=str(snapshot)
path=root/f'window_audit_v3_{last:04d}.json';payload=json.dumps(report,indent=2)
if path.exists():assert path.read_text()==payload,'Completed-window report changed'
else:path.write_text(payload)
print(json.dumps(report))
