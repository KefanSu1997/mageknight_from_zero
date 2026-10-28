"""Actual archived behavior replay, CPU forward only; not an optimizer gate."""
import argparse
import json
from pathlib import Path
import time
import torch
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from solo_policy_inputs import SoloPolicyInputs
from solo_structured_policy import SoloStructuredPolicy


def check(evidence,collection):
    root=Path(__file__).resolve().parents[2];evidence=Path(evidence);collection=Path(collection)
    schema=PublicSchema(evidence/'public_dto_schema.json')
    assets=SoloTextAssets(evidence/'solo_text_assets_v2',root)
    builder=SoloPolicyInputs(schema,assets)
    torch.set_num_threads(1)
    model=SoloStructuredPolicy(schema,assets.catalog.signature)
    model.load_state_dict(torch.load(collection/'behavior_policy.pt',weights_only=True))
    model.eval();reports=[];started=time.perf_counter()
    for path in sorted(collection.glob('episode_*/native.json')):
        records=json.loads(path.read_text())['native']['records']
        prepared=[builder.prepare_record(r) for r in records]
        actions=[r['SelectedIndex'] for r in records]
        with torch.inference_mode():
            values,logs,entropy,memory=model.evaluate_sequence(prepared,actions)
            old_values=values.new_tensor([r['ValuePrediction'] for r in records])
            old_logs=logs.new_tensor([r['BehaviorLogProbability'] for r in records])
            torch.testing.assert_close(values,old_values,rtol=0,atol=1e-7)
            torch.testing.assert_close(logs,old_logs,rtol=0,atol=1e-6)
            assert torch.isfinite(entropy).all() and (entropy>=0).all()
            # Splitting inference while carrying the true memory must preserve
            # the complete sequence; resetting every battle/round is not allowed.
            _,_,_,prefix=model.evaluate_sequence(prepared[:1],actions[:1])
            suffix,suffix_logs,_,_=model.evaluate_sequence(prepared[1:],actions[1:],prefix)
            torch.testing.assert_close(suffix,values[1:],rtol=0,atol=0)
            torch.testing.assert_close(suffix_logs,logs[1:],rtol=0,atol=0)
        reports.append(dict(path=str(path),decisions=len(records),
                            max_value_error=float((values-old_values).abs().max()),
                            max_logp_error=float((logs-old_logs).abs().max()),carry_memory_exact=True))
    assert reports
    report=dict(passed=True,episodes=reports,seconds=time.perf_counter()-started,
                scope='Archived neural partial sequences; forward equality only, no gradient/CUDA/PPO acceptance',training_started=False)
    (collection/'sequence_replay_gate.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps(report))


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('evidence');parser.add_argument('collection')
    args=parser.parse_args();check(args.evidence,args.collection)
