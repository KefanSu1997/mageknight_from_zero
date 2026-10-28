"""Tokenize every branching node of actual initial supports without truncation."""
import json
from pathlib import Path
from baseline_a_scenario_text import ScenarioText
from baseline_a_scenario_actions import prepare_support
from candidate_option_runtime import node_document
from hierarchical_option_policy import ChoiceTree
from joint_option_text import tokenizer,encode_document
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets


def run():
    root=Path('H:/MageKnight_RL_Runs/20261022_A_baseline_spec')
    states=json.loads((root/'native_observation_transport_states.json').read_text())
    schema=PublicSchema('H:/MageKnight_RL_Runs/20261021_solo_conquest_prepare/public_dto_schema.json')
    assets=SoloTextAssets('H:/MageKnight_RL_Runs/20261021_solo_conquest_prepare/solo_text_assets_v3','.')
    renderer=ScenarioText(schema,assets);tok=tokenizer();lengths=[];failures=[];example=None
    for index,native in enumerate(states):
        support=prepare_support(native['observation'],native['commands'],schema,assets)
        base=renderer.render(native['observation'],support);tree=ChoiceTree(support['operations'])
        for node,row in enumerate(tree.nodes):
            if len(row['children'])<=1:continue
            document=node_document(tree,base,node);example=example or document
            try:
                ids,positions=encode_document(tok,document,8192)
                assert len(positions)==len(row['children']) and len(set(positions))==len(positions)
                lengths.append(len(ids))
            except ValueError as exc:failures.append(dict(state=index,node=node,error=str(exc)))
    (root/'scenario_text_example.json').write_text(json.dumps(example,ensure_ascii=False,indent=2),encoding='utf-8')
    return dict(passed=bool(lengths) and not failures,states=len(states),branch_nodes=len(lengths),
                maximum_tokens=max(lengths,default=0),mean_tokens=sum(lengths)/max(len(lengths),1),failures=failures,
                scope='Actual tokenizer and entry supports only; no encoder/PPO or longest full-game context acceptance')

if __name__=='__main__':
    result=run();Path('H:/MageKnight_RL_Runs/20261022_A_baseline_spec/scenario_text_gate.json').write_text(json.dumps(result,indent=2));print(json.dumps(result))
