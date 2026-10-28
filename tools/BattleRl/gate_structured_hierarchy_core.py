"""Remote CPU mathematical gate over actual archived legal supports."""
import ast,json,hashlib,sys
from pathlib import Path
sys.path[:0]=['optimization_stage_20261014','tools/BattleRl']
import torch
from structured_hierarchy_core import StructuredHierarchy
torch.set_num_threads(1);torch.manual_seed(7112026)
source=Path('tools/BattleRl/hierarchical_option_policy.py').read_text();module=ast.parse(source)
nodes=[n for n in module.body if isinstance(n,(ast.FunctionDef,ast.ClassDef)) and n.name in ('operation_path','ChoiceTree')]
ns={};exec(compile(ast.Module(body=nodes,type_ignores=[]),'<tree>','exec'),ns)
cases=[];digest=hashlib.sha256()
with Path('remote_public_state71/contexts/contexts.jsonl').open('rb') as f:
    for raw in f:
        if not raw.endswith(b'\n'):break
        doc=json.loads(raw)
        if not doc.get('operations'):continue
        digest.update(raw);tree=ns['ChoiceTree'](doc['operations'])
        logits=torch.randn(tree.leaves,dtype=torch.float64,requires_grad=True)
        structured=StructuredHierarchy(tree,logits)
        actual=torch.stack([structured.log_prob(i) for i in range(tree.leaves)])
        expected=logits.log_softmax(0)
        error=float((actual-expected).abs().max().detach());assert error<1e-10
        weights=torch.randn_like(logits)
        g1=torch.autograd.grad((actual*weights).sum(),logits,retain_graph=True)[0]
        g2=torch.autograd.grad((expected*weights).sum(),logits)[0]
        grad_error=float((g1-g2).abs().max());assert grad_error<1e-9
        # Nonzero token residuals remain normalized with complete support.
        offsets={i:torch.randn(len(n['children']),dtype=torch.float64) for i,n in enumerate(tree.nodes) if n['children']}
        altered=StructuredHierarchy(tree,logits.detach(),lambda n:offsets[n])
        probs=torch.stack([altered.log_prob(i) for i in range(tree.leaves)]).exp()
        assert abs(float(probs.sum())-1)<1e-10 and bool((probs>0).all())
        leaf,path=altered.sample();assert 0<=leaf<tree.leaves and all(a[0]<b[0] for a,b in zip(path,path[1:]))
        cases.append(dict(leaves=tree.leaves,logprob_error=error,gradient_error=grad_error))
        if len(cases)==64:break
assert len(cases)==64
report=dict(cases=cases,context_prefix_sha=digest.hexdigest(),max_leaves=max(c['leaves'] for c in cases),max_logprob_error=max(c['logprob_error'] for c in cases),max_gradient_error=max(c['gradient_error'] for c in cases),scope='Random numeric leaf scores on 64 actual legal supports. Mathematical factorization/gradient gate only; not green checkpoint equivalence, token integration, CUDA update or training.')
out=Path('optimization_stage_20261014/structured_hierarchy_core_gate.json');out.write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k!='cases'}))
