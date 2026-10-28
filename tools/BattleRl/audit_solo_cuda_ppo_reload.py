"""Independent audit of a preserved completed optimizer checkpoint; no update."""
import hashlib
import json
from pathlib import Path
import time
import numpy as np
import torch
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from solo_policy_inputs import SoloPolicyInputs
from solo_structured_policy import SoloStructuredPolicy
from solo_episode_data import SoloEpisodeData,state_signature
from solo_ppo_update import SoloPPOUpdate

root=Path(__file__).resolve().parent;torch.set_num_threads(1);start=time.perf_counter()
manifest=json.loads((root/'manifest.json').read_text())
for name,sha in manifest['files'].items():assert hashlib.sha256((root/name).read_bytes()).hexdigest()==sha,name
checkpoint_path=root/'checkpoint.pt';checkpoint_sha=hashlib.sha256(checkpoint_path.read_bytes()).hexdigest()
state=torch.load(checkpoint_path,map_location='cuda',weights_only=False)
schema=PublicSchema(root/'public_dto_schema.json');assets=SoloTextAssets(root/'text_assets',root)
builder=SoloPolicyInputs(schema,assets)
episode=SoloEpisodeData(root/'collection/episode_000000/result.json')
original=torch.load(root/'collection/behavior_policy.pt',map_location='cpu',weights_only=True)
assert state_signature(original)!=state_signature(state['network'])
steps=int(state['accepted_steps']);assert steps>0
adam_steps=[]
for row in state['optimizer']['state'].values():
    adam_steps.append(int(row['step']))
    for value in row.values():
        if torch.is_tensor(value):assert torch.isfinite(value).all()
assert adam_steps and max(adam_steps)==steps
models=[]
for _ in range(2):
    updater=SoloPPOUpdate(SoloStructuredPolicy(schema,assets.catalog.signature),device='cuda')
    updater.policy.network.load_state_dict(state['network'])
    updater.policy.optimizer.load_state_dict(state['optimizer'])
    assert state_signature(updater.policy.network.state_dict())==state_signature(state['network'])
    models.append(updater.policy.network.eval())
with torch.inference_mode():
    values,logs,_,_=models[0].evaluate_sequence(episode.prepared_steps(builder),episode.actions)
    again,again_logs,_,_=models[0].evaluate_sequence(episode.prepared_steps(builder),episode.actions)
    other,other_logs,_,_=models[1].evaluate_sequence(episode.prepared_steps(builder),episode.actions)
for x,y in ((values,again),(logs,again_logs),(values,other),(logs,other_logs)):
    torch.testing.assert_close(x,y,rtol=1e-5,atol=1e-6)
predicted=values.cpu().numpy();target=episode.returns;var=float(np.var(target))
report=dict(passed=True,optimizer_steps=steps,adam_states=len(adam_steps),adam_step_min=min(adam_steps),adam_step_max=max(adam_steps),
            checkpoint_sha256=checkpoint_sha,parameter_sha256=state_signature(state['network']),parameter_changed=True,
            same_model_value_difference=float((values-again).abs().max()),same_model_logp_difference=float((logs-again_logs).abs().max()),
            reload_value_difference=float((values-other).abs().max()),reload_logp_difference=float((logs-other_logs).abs().max()),
            tolerance=dict(rtol=1e-5,atol=1e-6),bitwise_equal_claim=False,
            target_variance=var,post_ev=None if var<1e-12 else float(1-np.var(target-predicted)/var),
            post_mse=float(np.mean((target-predicted)**2)),post_bias=float(np.mean(predicted-target)),
            audit_seconds=time.perf_counter()-start,optimization_seconds=None,
            timing_limitation='Original gate failed after saving; optimization timing was not persisted, not reconstructed',
            formal_training_started=False,scope='Completed controlled-scenario PPO checkpoint audit; not EV threshold or formal experiment acceptance')
assert hashlib.sha256(checkpoint_path.read_bytes()).hexdigest()==checkpoint_sha
(root/'reload_audit.json').write_text(json.dumps(report,indent=2,allow_nan=False))
print(json.dumps(report,allow_nan=False),flush=True)
