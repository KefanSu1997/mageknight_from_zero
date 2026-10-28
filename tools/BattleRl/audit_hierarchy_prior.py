"""Structural prior on archived real supports; no model inference or evaluation."""
import ast,json,math,hashlib,statistics
from pathlib import Path
source=Path('tools/BattleRl/hierarchical_option_policy.py').read_text()
module=ast.parse(source)
selected=[n for n in module.body if isinstance(n,(ast.FunctionDef,ast.ClassDef)) and n.name in ('operation_path','ChoiceTree')]
namespace={};exec(compile(ast.Module(body=selected,type_ignores=[]),'<tree>','exec'),namespace)
root=Path('remote_public_state71');rows=[];digest=hashlib.sha256()
with (root/'contexts/contexts.jsonl').open('rb') as stream:
    for raw in stream:
        if not raw.endswith(b'\n'):break
        doc=json.loads(raw)
        if not doc.get('operations'):continue
        digest.update(raw);tree=namespace['ChoiceTree'](doc['operations'])
        probabilities=[];depths=[]
        for route in tree.paths:
            counts=[len(tree.nodes[node]['children']) for node,_ in route if len(tree.nodes[node]['children'])>1]
            probabilities.append(math.prod(1/n for n in counts));depths.append(len(counts))
        assert abs(sum(probabilities)-1)<1e-8
        entropy=-sum(p*math.log(p) for p in probabilities)
        node=tree.branch(0);branches=tree.nodes[node]['children']
        rows.append(dict(leaves=tree.leaves,max_depth=max(depths),max_to_min_probability=max(probabilities)/min(probabilities),effective_leaf_fraction=math.exp(entropy)/tree.leaves,root_options=[list(edge) for edge in branches]))
        if len(rows)>=256:break
assert rows
report=dict(contexts=len(rows),source_prefix_sha=digest.hexdigest(),tree_source_sha=hashlib.sha256(source.encode()).hexdigest(),median_effective_leaf_fraction=statistics.median(r['effective_leaf_fraction'] for r in rows),max_probability_ratio=max(r['max_to_min_probability'] for r in rows),rows=rows,scope='Uniform conditional branches structural prior only, not actual trained action probabilities. Unequal leaf mass is not by itself an implementation bug; no causal claim.')
target=root/'hierarchy_prior_first256.json'
payload=json.dumps(report,indent=2)
if target.exists():assert target.read_text()==payload
else:target.write_text(payload)
print(json.dumps({k:v for k,v in report.items() if k!='rows'}))
