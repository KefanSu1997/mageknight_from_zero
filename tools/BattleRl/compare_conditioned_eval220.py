"""Pair complete fixed evaluations by exact task ID, retaining all failures."""
import hashlib
import json
from pathlib import Path

old_path = Path('remote_context_pilot200/eval_0200/summary.json')
new_path = Path('remote_conditioned300/eval_0220/summary.json')
if not new_path.exists():
    print(json.dumps(dict(state='waiting_complete_summary')))
    raise SystemExit(0)
old, new = [json.loads(p.read_text()) for p in (old_path, new_path)]
for doc in (old, new):
    assert doc['episodes'] == 1000 and len(doc['records']) == 1000
    assert doc['frozen_parameters'] and doc['training_rng_restored']
    assert doc['per_task_actor_seed']
left, right = [{r['task_id']: r for r in d['records']} for d in (old, new)]
assert len(left) == len(right) == 1000 and left.keys() == right.keys()
transitions = {'both_win': [], 'gained_win': [], 'lost_win': [], 'both_fail': []}
reward_differences = []
for task in sorted(left):
    a, b = left[task], right[task]
    key = ('both_win' if a['won'] else 'gained_win') if b['won'] else ('lost_win' if a['won'] else 'both_fail')
    transitions[key].append(task)
    reward_differences.append(float(b['reward']) - float(a['reward']))
assert sum(bool(r['won']) for r in left.values()) == old['wins']
assert sum(bool(r['won']) for r in right.values()) == new['wins']
result = dict(state='complete', old_update=old['update'], new_update=new['update'],
              old_wins=old['wins'], new_wins=new['wins'],
              counts={k: len(v) for k, v in transitions.items()},
              mean_reward_change=sum(reward_differences)/1000,
              old_performance=old['performance'], new_performance=new['performance'],
              paired_task_ids=transitions,
              source_sha={str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in (old_path, new_path)},
              scope='Same fixed1000/task actor seeds, single training trajectory. No multi-seed significance or single-factor causal claim.')
Path('optimization_stage_20261014/conditioned_eval220_paired.json').write_text(json.dumps(result, indent=2))
print(json.dumps({k: v for k, v in result.items() if k != 'paired_task_ids'}))
