"""Count archived executed trace actions; boundary power is not attack count."""
import argparse,json,hashlib,collections
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--root',required=True);p.add_argument('--start',type=int,required=True);p.add_argument('--end',type=int,required=True);a=p.parse_args()
root=Path(a.root);groups=collections.defaultdict(collections.Counter);files=[];episodes=collections.defaultdict(set);enemy_shape_changes=0
batches=[json.loads(x) for x in (root/'episode_batches.jsonl').read_text().splitlines()]
selected=[b for b in batches if a.start<=b['update']<=a.end]
assert len(selected)==a.end-a.start+1
for b in selected:
    assert b['accepted_episodes']==128
    for e in b['episodes']:
        path=root/'episodes'/f"worker_{e['worker']}_episode_{e['episode']}.json"
        raw=path.read_bytes();d=json.loads(raw);assert d['certificate']['task_id']==e['task_id']
        files.append(dict(path=str(path),sha256=hashlib.sha256(raw).hexdigest()))
        for t in d['trace']:
            before,after=t['before'],t['after'];phase=before['phase'];kind=t['action']['Kind']
            groups[phase][kind]+=1;episodes[(phase,kind)].add(str(path))
            if len(before['enemies'])==len(after['enemies']):
                # Aggregate defeated-count increase only, without assuming slot identity.
                delta=sum(x['defeated'] for x in after['enemies'])-sum(x['defeated'] for x in before['enemies'])
                if delta>0:groups[phase]['observed_defeated_increase']+=delta
            else:enemy_shape_changes+=1
assert len(files)==128*len(selected)
report=dict(updates=[a.start,a.end],episodes=len(files),trace_action_counts={k:dict(v) for k,v in groups.items()},episodes_with_action={phase:{kind:len(v) for (ph,kind),v in episodes.items() if ph==phase} for phase in groups},enemy_shape_changes=enemy_shape_changes,files=files,scope='Executed archived trace entries; may include atomic micro-operations, not PPO decision count. Defeated count changes are observed results, not inferred from remaining power. Different task streams, no causal comparison.')
target=root/f'phase_actions_{a.start:04d}_{a.end:04d}.json';payload=json.dumps(report,indent=2)
if target.exists():assert target.read_text()==payload
else:target.write_text(payload)
print(json.dumps({k:v for k,v in report.items() if k!='files'}))
