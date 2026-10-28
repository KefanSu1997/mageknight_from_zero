"""Summarize a retained training checkpoint's behavior without simulating extra games."""
import argparse,hashlib,json
from collections import Counter
from pathlib import Path

p=argparse.ArgumentParser();p.add_argument('run');p.add_argument('--at-steps',type=int,required=True);a=p.parse_args()
root=Path(a.run);rows=[]
for file in sorted(root.glob(f'audit_{a.at_steps}_*.json')):
    saved=json.loads(file.read_text(encoding='utf-8'));trace=saved['trace'];kinds=Counter(t['action']['Kind'] for t in trace)
    streak=longest=planning_count=0;planning={'unitDamage','defend','clearDefend','bowChoice','killCrystal','protectionCleanup','roundReturn'}
    for t in trace:
        action=t['action'];is_planning=action['Kind'] in planning or action['Kind']=='bannerUse' and action['Card']=='items_018'
        planning_count+=is_planning;streak=streak+1 if is_planning else 0;longest=max(longest,streak)
    rows.append({'seed':saved['seed'],'source':str(file),'source_sha256':hashlib.sha256(file.read_bytes()).hexdigest(),'operations':len(trace),'won_battles':sum(t['after']['won'] and not t['before']['won'] for t in trace),'started_battles':kinds['fight'],'stop_reason':trace[-1]['after']['stopReason'],'final_phase':trace[-1]['after']['phase'],'planning_operations':planning_count,'longest_consecutive_planning':longest,'artifact_modes':sorted({t['action']['Card']+':'+t['action']['Choice'] for t in trace if t['action']['Kind']=='artifact'})})
assert rows,'No actual retained trajectories at requested checkpoint'
result={'scope':'retained checkpoint subset; different seeds are not a controlled before/after evaluation','checkpoint_steps':a.at_steps,'trajectories':len(rows),'operations':sum(r['operations'] for r in rows),'won_battles':sum(r['won_battles'] for r in rows),'started_battles':sum(r['started_battles'] for r in rows),'step_limit_episodes':sum(r['stop_reason']=='step_limit' for r in rows),'no_battle_episodes':sum(r['started_battles']==0 for r in rows),'longest_consecutive_planning':max(r['longest_consecutive_planning'] for r in rows),'rows':rows}
out=root/f'behavior_review_{a.at_steps}.json';out.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='rows'},ensure_ascii=False))
