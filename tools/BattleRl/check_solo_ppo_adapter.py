"""CPU forward/batch contract only. No optimizer step is executed locally."""
import argparse
import json
from pathlib import Path
from types import SimpleNamespace
import numpy as np
import torch
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from solo_policy_inputs import SoloPolicyInputs
from solo_structured_policy import SoloStructuredPolicy
from solo_episode_data import SoloEpisodeData,verify_behavior_binding
from solo_ppo_update import SoloPPOUpdate,SoloSequenceBuffer


def check(evidence,gate):
    evidence=Path(evidence);gate=Path(gate);root=Path(__file__).resolve().parents[2]
    torch.set_num_threads(1)
    schema=PublicSchema(evidence/'public_dto_schema.json');assets=SoloTextAssets(evidence/'solo_text_assets_v2',root)
    builder=SoloPolicyInputs(schema,assets)
    network=SoloStructuredPolicy(schema,assets.catalog.signature)
    network.load_state_dict(torch.load(gate/'neural_2/behavior_policy.pt',weights_only=True))
    updater=SoloPPOUpdate(network,device='cpu')
    # Partial sequences are used for likelihood comparison only, never update_complete.
    episodes=[]
    for path in sorted((gate/'neural_2').glob('episode_*/native.json')):
        rows=json.loads(path.read_text())['native']['records']
        e=SimpleNamespace(records=rows,actions=np.array([r['SelectedIndex'] for r in rows]),
                          values=np.array([r['ValuePrediction'] for r in rows]),
                          old_log_probs=np.array([r['BehaviorLogProbability'] for r in rows]),
                          returns=np.zeros(len(rows)),advantages=np.zeros(len(rows)))
        episodes.append(e)
    with torch.inference_mode():
        for batch in SoloSequenceBuffer(episodes,builder,'cpu',17).get(128):
            values,logs,entropy=updater.policy.evaluate_actions(batch.observations,batch.actions,None,None)
            torch.testing.assert_close(values,batch.old_values,rtol=0,atol=1e-7)
            torch.testing.assert_close(logs,batch.old_log_prob,rtol=0,atol=1e-6)
            assert torch.isfinite(entropy).all()
    complete=[SoloEpisodeData(p) for p in sorted((gate/'clock').glob('episode_*/result.json'))]
    batches=list(SoloSequenceBuffer(complete,builder,'cpu',17).get(128))
    assert sorted(len(b.actions) for b in batches)==[2,128]
    seen=[(id(e),i) for b in batches for e,i in b.observations[0]]
    assert len(seen)==len(set(seen))==130
    try:verify_behavior_binding(complete[0],network,builder)
    except ValueError as error:assert 'Scripted' in str(error)
    else:raise AssertionError('Scripted trajectory accepted for on-policy update')
    # Regression for the old tail-one unbiased-std NaN: no transition dropped.
    fixture=SimpleNamespace(records=[None]*129,actions=np.zeros(129,dtype=int),values=np.zeros(129),
                            returns=np.zeros(129),advantages=np.zeros(129),old_log_probs=np.zeros(129))
    assert [len(b.actions) for b in SoloSequenceBuffer([fixture],builder,'cpu',17).get(128)]==[129]
    try:updater.update_complete(complete,builder,17)
    except ValueError as error:assert 'remote CUDA' in str(error)
    else:raise AssertionError('Local RL optimization accepted')
    assert updater.policy.accepted_steps==0
    result=dict(passed=True,neural_replay_steps=6,batch_sizes=[128,2],singleton_tail_merged=129,
                complete_samples_covered_once=130,scripted_policy_rejected=True,optimizer_steps=0,
                training_started=False,scope='Upstream optimizer adapter forward/batch contract; CUDA PPO still pending')
    (gate/'ppo_adapter_gate.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps(result))


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('evidence');parser.add_argument('gate')
    args=parser.parse_args();check(args.evidence,args.gate)
