"""Exhaustive off-policy entropy-gradient gate on uneven hierarchical trees.

CPU only; no game/model changes. Includes forced edges and shared parameters.
"""
import json
from pathlib import Path
import torch

torch.set_num_threads(1)
# Each internal node has its own legal branching factor; leaves have no children.
children = {0: [1, 2, 3], 1: [4], 2: [5, 6], 3: [7, 8, 9], 6: [10, 11]}
generator = torch.Generator().manual_seed(7112026)
weights = {n: torch.randn(len(c), 7, generator=generator, dtype=torch.float64)
           for n, c in children.items()}

def leaf_logs(x):
    logs = {n: (w @ x).log_softmax(0) for n, w in weights.items()}
    leaves = []
    def visit(node, total, root_log):
        if node not in children:
            leaves.append((node, total, root_log))
            return
        for i, child in enumerate(children[node]):
            lp = logs[node][i]
            visit(child, total + lp, lp if node == 0 else root_log)
    visit(0, x.sum() * 0, None)
    return leaves, logs[0]

reports = []
for old_scale, new_scale in [(0., 1.), (1., .3), (2., 1.5), (.2, 3.)]:
    old_x = torch.randn(7, generator=generator, dtype=torch.float64) * old_scale
    x = (torch.randn(7, generator=generator, dtype=torch.float64) * new_scale).requires_grad_()
    old_leaves, _ = leaf_logs(old_x)
    leaves, root_log = leaf_logs(x)
    old_log = torch.stack([r[1] for r in old_leaves]).detach()
    logp = torch.stack([r[1] for r in leaves])
    assert [r[0] for r in leaves] == [r[0] for r in old_leaves]
    assert abs(float(logp.exp().sum().detach()) - 1) < 1e-12
    exact = -(logp.exp() * logp).sum()
    truth = torch.autograd.grad(exact, x, retain_graph=True)[0]
    root_h = -(root_log.exp() * root_log).sum()
    samples = torch.stack([root_h + (lp - old_log[i]).exp() * (rp - lp)
                           for i, (_, lp, rp) in enumerate(leaves)])
    expected = (old_log.exp() * samples).sum()
    actual = torch.autograd.grad(expected, x)[0]
    error = float((truth - actual).abs().max())
    value_error = abs(float((exact - expected).detach()))
    assert error < 1e-10 and value_error < 1e-10
    reports.append(dict(old_scale=old_scale, new_scale=new_scale,
                        gradient_max_error=error, entropy_error=value_error))
result = dict(passed=True, leaves=len(leaves), cases=reports,
              scope='Uneven 3-level trees, forced branch, shared parameters, off-policy exact enumeration. No CUDA/game gate or speed/learning claim.')
Path('optimization_stage_20261014/root_entropy_offpolicy_gate.json').write_text(json.dumps(result, indent=2))
print(json.dumps(result))
