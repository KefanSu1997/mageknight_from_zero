"""Remote CPU distribution integration gate; synthetic marker vectors, real trees."""
import json, sys
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch
sys.path[:0]=['optimization_stage_20261014','tools/BattleRl']
import numpy as np
import torch
import structured_joint_policy as s
torch.set_num_threads(1);torch.manual_seed(7112026)
cases=[]
for raw in Path('remote_public_state71/contexts/contexts.jsonl').open():
    doc=json.loads(raw)
    if not doc.get('operations'):continue
    tree=s.h.ChoiceTree(doc['operations']);k=tree.leaves
    if k<2:continue
    policy=SimpleNamespace(context_path='unused',registry_file='unused',_supports=[1],
        marker_options=torch.nn.Linear(384,128).double(),marker_query=torch.nn.Linear(128,128).double())
    torch.nn.init.zeros_(policy.marker_query.weight);torch.nn.init.zeros_(policy.marker_query.bias)
    latent=torch.randn(1,128,dtype=torch.float64,requires_grad=True)
    scores=torch.randn(1,k,dtype=torch.float64,requires_grad=True)
    flat=SimpleNamespace(distribution=torch.distributions.Categorical(logits=scores))
    vectors={n:np.random.default_rng(n).normal(size=(len(d['children'])+1,384)) for n,d in enumerate(tree.nodes)}
    with patch.object(s.h,'tree',return_value=(tree,doc)),patch.object(s,'registry',return_value=SimpleNamespace(get=lambda _:doc['sequences'])),patch.object(s.h,'node_vectors',side_effect=lambda p,c,n:vectors[n]):
        dist=s.StructuredJointDistribution(policy,latent,[1],flat)
        logp=torch.stack([dist.log_prob(torch.tensor([a]))[0] for a in range(k)])
        error=float((logp-scores.log_softmax(-1)[0]).abs().max().detach());assert error<1e-10
        # Exact expectation of the importance-weighted estimator must have the
        # same gradient as categorical entropy, including away from old policy.
        old=torch.randn(k,dtype=torch.float64).log_softmax(0).detach()
        estimate=(old.exp()*(-(logp-old).exp()*logp)).sum()
        exact=-(logp.exp()*logp).sum()
        g1=torch.autograd.grad(estimate,scores,retain_graph=True)[0]
        g2=torch.autograd.grad(exact,scores,retain_graph=True)[0]
        entropy_error=float((g1-g2).abs().max());assert entropy_error<1e-10
        logp[0].backward();assert float(policy.marker_query.weight.grad.norm())>0
        with torch.no_grad():policy.marker_query.weight.normal_(std=.01)
        altered=s.StructuredJointDistribution(policy,latent,[1],flat)
        probs=torch.stack([altered.log_prob(torch.tensor([a]))[0] for a in range(k)]).exp()
        assert abs(float(probs.sum().detach())-1)<1e-10 and bool((probs>0).all())
        cases.append(dict(leaves=k,logprob_error=error,entropy_gradient_error=entropy_error))
    if len(cases)==16:break
assert len(cases)==16
report=dict(cases=cases,scope='Actual StructuredJointDistribution with archived trees; synthetic marker vectors and leaf scores. No pretrained encoding, environment rollout, CUDA update or training proof.')
Path('optimization_stage_20261014/structured_joint_distribution_gate.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
