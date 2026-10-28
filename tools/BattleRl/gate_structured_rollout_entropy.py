"""Remote-only runner binding gate; no environment collection or model training."""
import json
from pathlib import Path
from types import SimpleNamespace
import numpy as np
from structured_rollout_entropy import bound_rollout_entropy, rollout_reference

b = SimpleNamespace(observations={'context_id': np.array([[[1]], [[2]], [[0]]])},
                    actions=np.array([[[3]], [[4]], [[0]]]),
                    log_probs=np.array([[-.7], [-1.2], [0.]]))
m = SimpleNamespace(policy=SimpleNamespace(), rollout_buffer=b)
with bound_rollout_entropy(m):
    assert m.policy._entropy_reference == {(1, 3): -.7, (2, 4): -1.2}
    try:
        with bound_rollout_entropy(m):
            raise AssertionError('Nested binding accepted')
    except RuntimeError:
        pass
assert m.policy._entropy_reference is None
try:
    with bound_rollout_entropy(m):
        raise RuntimeError('Injected upstream failure')
except RuntimeError:
    pass
assert m.policy._entropy_reference is None
for contexts, actions, probs in [([1, 1], [3, 3], [-.7, -.8]),
                                ([1.5], [3], [-.7]), ([1], [3], [np.nan])]:
    bad = SimpleNamespace(observations={'context_id': np.array(contexts)},
                          actions=np.array(actions), log_probs=np.array(probs))
    try:
        rollout_reference(bad)
    except ValueError:
        continue
    raise AssertionError('Invalid binding accepted')
report = dict(passed=True, checks=['exact binding', 'padding excluded', 'nested binding refused',
                                 'exception cleanup', 'likelihood collision refused',
                                 'fractional identity refused', 'nonfinite refused'],
              scope='Runner reference lifecycle only. No pretrained encoding, CUDA PPO update, checkpoint or policy benefit proved.')
Path('optimization_stage_20261014/structured_rollout_entropy_gate.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report))
