"""Index actionable trajectory evidence; never treats a legal loss as corrupt data."""
from collections import Counter
import json
import math


def review_episodes(episodes):
    rows=[]
    for episode in episodes:
        branches=Counter();errors=[]
        for index,record in enumerate(episode.records):
            command=json.loads(record['CommandsJson'][record['SelectedIndex']])
            branches.update(k for k,v in command.items() if v is not None and k not in ('SchemaVersion','ExpectedStep'))
            delta=float(episode.returns[index]-episode.values[index])
            errors.append((abs(delta),index,delta))
        final=episode.records[-1]['Transition']['Score']
        warnings=episode.result.get('audit',{}).get('warnings',[])
        rows.append(dict(task_id=episode.result['task_id'],archive=episode.result['archive'],
            decisions=len(episode.records),return_scaled=float(sum(episode.rewards)),
            final_score=final['Total'],fame=final['Inventory']['Fame'],cities=final['Cities'],
            final_deck_wounds=final['Inventory']['DeckWounds'],branches=dict(branches),
            public_revisits=warnings,
            largest_value_errors=[dict(step=i,residual=d) for _,i,d in sorted(errors,reverse=True)[:5]]))
    if not rows:raise ValueError('Need actual completed trajectories')
    # Keep complete per-task summaries; selection only chooses where human/agent
    # inspection begins, never which episodes count for training or evaluation.
    selected=set()
    for key,reverse in [('return_scaled',True),('return_scaled',False),('final_deck_wounds',True)]:
        selected.update(r['task_id'] for r in sorted(rows,key=lambda r:r[key],reverse=reverse)[:3])
    selected.update(r['task_id'] for r in rows if r['public_revisits'])
    selected.add(rows[len(rows)//2]['task_id'])
    positives=[dict(task_id=r['task_id'],archive=r['archive'],cycle=w) for r in rows
               for w in r['public_revisits'] if w['cycle_return']>1e-8]
    return dict(episodes=len(rows),tasks=rows,inspect_first=sorted(selected),
                positive_public_cycle_candidates=positives,
                status='review_required' if positives else 'no_positive_public_cycle_detected',
                proves_no_hacking=False,
                limitations='Public-state cycles may hide RNG/pile changes; inspect native replay before assigning cause')
