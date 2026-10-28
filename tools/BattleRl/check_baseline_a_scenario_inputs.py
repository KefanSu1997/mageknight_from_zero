"""Combined real native input and A-policy wiring; controlled text vectors."""
import copy,json
from pathlib import Path
import numpy as np
from baseline_a_scenario_inputs import ScenarioInputs
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets


def run():
    root=Path('H:/MageKnight_RL_Runs/20261022_A_baseline_spec')
    states=json.loads((root/'native_observation_transport_states.json').read_text())
    schema=PublicSchema('H:/MageKnight_RL_Runs/20261021_solo_conquest_prepare/public_dto_schema.json')
    assets=SoloTextAssets('H:/MageKnight_RL_Runs/20261021_solo_conquest_prepare/solo_text_assets_v3','.')
    ids=json.loads(Path('H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets/manifest.json').read_text())['entity_ids']
    inputs=ScenarioInputs('.',schema,assets,ids);maximum=0;count=0
    for native in states:
        observation,support=inputs.encode(native)
        assert support['commands']==tuple(native['commands'])
        np.testing.assert_array_equal(observation['state'][:6314],np.asarray(native['baseline_a_features'],np.float32))
        assert not observation['state'][6314:6411].any()
        history=np.arange(97,dtype=np.float32)/100
        with_history,_=inputs.encode(native,history)
        np.testing.assert_array_equal(with_history['state'][6314:6411],history)
        maximum=max(maximum,int(observation['entity_present'].sum()));count+=len(support['commands'])
    for key in ('support_sha256','baseline_a_counter_schema'):
        bad=copy.deepcopy(states[0]);bad[key]='wrong'
        try:inputs.encode(bad)
        except ValueError:pass
        else:raise AssertionError('Binding failure not rejected')
    try:ScenarioInputs('.',schema,assets,ids,capacity=1).encode(states[0])
    except ValueError:pass
    else:raise AssertionError('Overflow silently truncated')
    return dict(passed=True,real_native_states=len(states),complete_candidates=count,maximum_entities=maximum,
                state_width=6471,previous_history_exact=True,negative_cases=3,optimizer_steps=0,
                scope='Seven-hero entry native full-input assembly; not full-game policy/CUDA/PPO or actual joint-text reader acceptance')

if __name__=='__main__':
    result=run();Path('H:/MageKnight_RL_Runs/20261022_A_baseline_spec/scenario_inputs_gate.json').write_text(json.dumps(result,indent=2));print(json.dumps(result))
