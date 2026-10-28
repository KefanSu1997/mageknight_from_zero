"""Audit public resources on actual combat entry; no hidden-state inference."""
import argparse
import hashlib
import json
from pathlib import Path

def decoded(value):
    return json.loads(value) if isinstance(value,str) else value

def review(root):
    rows=[];episodes=0
    for path in sorted(Path(root).glob('episode_*/native.json')):
        result_path=path.with_name('result.json')
        if not result_path.exists():continue
        result=json.loads(result_path.read_text())
        if not result.get('eligible_complete_episode'):continue
        raw=path.read_bytes()
        assert hashlib.sha256(raw).hexdigest()==result['archive']['sha256']
        records=json.loads(raw)['native']['records'];episodes+=1
        for i,record in enumerate(records):
            before=decoded(record['ObservationBefore']);after=decoded(record['ObservationAfter'])
            if before['Combat']['Active'] or not after['Combat']['Active']:continue
            hand=after['Inventory']['Hand'];following=[]
            for future in records[i+1:]:
                if not decoded(future['ObservationBefore'])['Combat']['Active']:break
                following.append(future)
            selected=[decoded(r['CommandsJson'][r['SelectedIndex']]) for r in following]
            rows.append(dict(task=result['task_id'],archive=str(path),step=record['BeforeStep'],
                command=decoded(record['CommandsJson'][record['SelectedIndex']]),hand=hand,
                enemies=after['Combat']['Enemies'],units=after['Inventory']['Units'],
                non_wound_hand=sum(c['Type']!='Wound' for c in hand),
                forced_combat_decisions=sum(len(r['CommandsJson'])==1 for r in following),
                combat_decisions=len(following),
                attack_selected=sum(c.get('Attack') is not None for c in selected),
                attack_available=sum(any(decoded(c).get('Attack') is not None for c in r['CommandsJson']) for r in following),
                reward_during_combat=sum(r['Transition']['Reward'] for r in following)))
    return dict(complete_episodes=episodes,entries=len(rows),
                entries_empty_hand=sum(not r['hand'] for r in rows),
                entries_no_nonwound_hand=sum(r['non_wound_hand']==0 for r in rows),
                entries_no_attack_available=sum(r['attack_available']==0 for r in rows),rows=rows,
                scope='Completed training trajectories, public resource and legal-action evidence only')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--root',required=True);p.add_argument('--out',required=True)
    a=p.parse_args();r=review(a.root)
    Path(a.out).write_text(json.dumps(r,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in r.items() if k!='rows'}))
