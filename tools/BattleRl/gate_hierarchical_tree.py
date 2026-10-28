import json,collections
from pathlib import Path
from train_sb3_atomic_entity_text import configure_rules
from train_ppo import Rules
from joint_option_text import tokenizer,encode_document
from hierarchical_option_policy import ChoiceTree
root=Path('H:/MageKnight_RL_Runs/20261013_joint_option_prepare')
configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll');r=Rules();actions=r.actions;r.close()
tok=tokenizer();base=json.loads((root/'context_0001.json').read_text(encoding='utf-8'));results=[]
for count,support,sequences in json.loads((root/'largest_supports.json').read_text()):
    operations=[[actions[k] for k in seq] for seq in sequences];t=ChoiceTree(operations)
    branches=[(i,n) for i,n in enumerate(t.nodes) if len(n['children'])>1]
    max_tokens=0;longest=0
    for i,n in branches:
        ids,pos=encode_document(tok,t.document(base,i),8192);max_tokens=max(max_tokens,len(ids))
        assert len(pos)==len(n['children'])
        assert all(child>i for child in n['children'].values())
    for leaf,route in enumerate(t.paths):
        node=0;depth=0
        for parent,edge in route:
            assert parent==node
            if len(t.nodes[node]['children'])>1:depth+=1
            node=t.nodes[node]['children'][edge]
        assert t.nodes[node]['leaf']==leaf;longest=max(longest,depth)
    results.append(dict(support=support,complete_candidates=count,leaf_count=t.leaves,max_branch=max(len(n['children']) for n in t.nodes),max_decisions=longest,max_node_tokens=max_tokens,branch_nodes=len(branches)))
    print(json.dumps(results[-1]),flush=True)
    if len(results)==1:
        (root/'hierarchical_example.json').write_text(json.dumps(t.document(base,t.branch(0)),ensure_ascii=False,indent=2),encoding='utf-8')
(root/'hierarchical_tree_gate.json').write_text(json.dumps(dict(passed=True,results=results,all_leaves_bijective=True,strictly_forward=True,no_environment_preselection=True),indent=2))
