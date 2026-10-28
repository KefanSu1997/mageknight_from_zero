import json, hashlib
from pathlib import Path

old = Path('remote_final_replay_old')
new = Path('remote_final_replay_ready')
a = json.loads((old / 'benchmark.json').read_text())
b = json.loads((new / 'benchmark.json').read_text())
assert a['parameter_sha'] == b['parameter_sha']
assert a['steps'] == b['steps'] == 789
x = json.loads((old / 'step_values.json').read_text())
y = json.loads((new / 'step_values.json').read_text())
assert x.keys() == y.keys() and len(x) == 789
errors = {name: max(abs(x[k][name] - y[k][name]) for k in x)
          for name in ('actions', 'rewards', 'values', 'returns', 'log_probs')}
assert all(errors[n] == 0 for n in ('actions', 'rewards')), errors
# BF16 encoder kernel batching differs at 16 versus 32 policy rows. Record
# tolerances explicitly; these are numeric agreement, not bitwise equality.
assert errors['returns'] <= 1e-6 and errors['values'] <= 1e-4 and errors['log_probs'] <= 1e-3, errors
count = 0
for path in sorted((old / 'episodes').glob('*episode*.json')):
    left = json.loads(path.read_text())
    right = json.loads((new / 'episodes' / path.name).read_text())
    for doc in (left, right):
        for row in doc['trace']:
            row.pop('support_id', None)
    assert left == right, path.name
    count += 1
assert count == 64
result = dict(passed=b['collection_seconds'] < a['collection_seconds'],
              episodes=count, steps=a['steps'], max_abs_errors=errors,
              tolerance=dict(actions=0,rewards=0,returns=1e-6,values=1e-4,log_probs=1e-3),
              old_seconds=a['collection_seconds'], new_seconds=b['collection_seconds'],
              speedup=a['collection_seconds'] / b['collection_seconds'],
              parameter_sha=a['parameter_sha'], parameters_unchanged=True,
              full_episode_documents_exact_except_local_support_ids=True,
              scope='Fixed real workload replay, no optimizer update. Formal training uses normal stochastic sampling.',
              candidate_sha=hashlib.sha256(Path('optimization_stage_20261014/ready_episode_collector.py').read_bytes()).hexdigest())
Path('optimization_stage_20261014/final_ready_gate.json').write_text(json.dumps(result, indent=2))
print(json.dumps(result))

