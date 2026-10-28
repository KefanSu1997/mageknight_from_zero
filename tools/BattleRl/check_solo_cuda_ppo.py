"""One SHA-bound native complete scenario through upstream CUDA PPO.

This is an integration gate on one controlled world, not the formal experiment.
"""
import hashlib
import inspect
import json
from pathlib import Path
import time
import traceback
import numpy as np
import torch
import sb3_contrib
from sb3_contrib import RecurrentPPO
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from solo_policy_inputs import SoloPolicyInputs
from solo_structured_policy import SoloStructuredPolicy
from solo_episode_data import SoloEpisodeData,state_signature
from solo_ppo_update import SoloPPOUpdate


def run():
    root=Path(__file__).resolve().parent; start=time.perf_counter()
    manifest=json.loads((root/'manifest.json').read_text())
    for name,sha in manifest['files'].items():
        assert hashlib.sha256((root/name).read_bytes()).hexdigest()==sha,name
    assert sb3_contrib.__version__=='2.7.0'
    assert torch.cuda.is_available()
    torch.set_num_threads(1);torch.manual_seed(7112026)
    schema=PublicSchema(root/'public_dto_schema.json');assets=SoloTextAssets(root/'text_assets',root)
    builder=SoloPolicyInputs(schema,assets)
    collection=root/'collection'; original=collection/'episode_000000/source_result.json'
    result=json.loads(original.read_text())
    # Explicit path portability only; the native archive remains byte-identical.
    result['archive']['path']=str(collection/'episode_000000/native.json')
    migrated=original.with_name('result.json');migrated.write_text(json.dumps(result,indent=2))
    episode=SoloEpisodeData(migrated)
    network=SoloStructuredPolicy(schema,assets.catalog.signature)
    network.load_state_dict(torch.load(collection/'behavior_policy.pt',map_location='cpu',weights_only=True))
    before=state_signature(network.state_dict())
    updater=SoloPPOUpdate(network,device='cuda')
    torch.cuda.reset_peak_memory_stats()
    print(json.dumps(dict(stage='starting_upstream_ppo',steps=len(episode.records),epochs=4,batch=128)),flush=True)
    update=updater.update_complete([episode],builder,7112026)
    (root/'update_result.json').write_text(json.dumps(update,indent=2,allow_nan=False))
    assert update['accepted_optimizer_steps']>0,update
    after=state_signature(network.state_dict());assert after!=before
    assert all(torch.isfinite(p).all() for p in network.parameters())
    checkpoint=dict(network=network.state_dict(),optimizer=updater.policy.optimizer.state_dict(),
                    torch_rng=torch.get_rng_state(),cuda_rng=torch.cuda.get_rng_state_all(),
                    minibatch_rng=updater.rollout_buffer.rng.bit_generator.state,
                    accepted_steps=updater.policy.accepted_steps)
    torch.save(checkpoint,root/'checkpoint.pt')
    with torch.inference_mode():
        values,logs,_,_=network.evaluate_sequence(episode.prepared_steps(builder),episode.actions)
    restored=SoloPPOUpdate(SoloStructuredPolicy(schema,assets.catalog.signature),device='cuda')
    state=torch.load(root/'checkpoint.pt',map_location='cuda',weights_only=False)
    restored.policy.network.load_state_dict(state['network'])
    restored.policy.optimizer.load_state_dict(state['optimizer'])
    assert state_signature(restored.policy.network.state_dict())==after
    with torch.inference_mode():
        rv,rl,_,_=restored.policy.network.evaluate_sequence(episode.prepared_steps(builder),episode.actions)
    torch.testing.assert_close(rv,values,rtol=1e-5,atol=1e-6)
    torch.testing.assert_close(rl,logs,rtol=1e-5,atol=1e-6)
    predicted=values.cpu().numpy();target=episode.returns;var=float(np.var(target))
    report=dict(passed=True,complete_scenarios=1,steps=len(episode.records),update=update,
                target_variance=var,post_ev=None if var<1e-12 else float(1-np.var(target-predicted)/var),
                post_mse=float(np.mean((target-predicted)**2)),post_bias=float(np.mean(predicted-target)),
                checkpoint_sha256=hashlib.sha256((root/'checkpoint.pt').read_bytes()).hexdigest(),
                parameter_before=before,parameter_after=after,reload_parameters_exact=True,
                reload_value_difference=float((rv-values).abs().max()),
                reload_logp_difference=float((rl-logs).abs().max()),
                reload_tolerance=dict(rtol=1e-5,atol=1e-6),
                adam_states=len(restored.policy.optimizer.state),peak_allocated_bytes=torch.cuda.max_memory_allocated(),
                full_gate_seconds=time.perf_counter()-start,
                upstream_train_sha256=hashlib.sha256(inspect.getsource(RecurrentPPO.train).encode()).hexdigest(),
                formal_training_started=False,scope='One controlled native scenario CUDA PPO integration; no learning/generalization/EV threshold acceptance')
    (root/'result.json').write_text(json.dumps(report,indent=2,allow_nan=False))
    print(json.dumps(report,allow_nan=False),flush=True)


if __name__=='__main__':
    try:run()
    except Exception as error:
        Path(__file__).with_name('failure.json').write_text(json.dumps(dict(error=repr(error),traceback=traceback.format_exc()),indent=2))
        raise
