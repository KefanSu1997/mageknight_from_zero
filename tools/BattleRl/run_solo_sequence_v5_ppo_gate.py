"""Same seven complete on-policy scenarios, batched forest CUDA PPO gate."""
import json,sys,time
from pathlib import Path
import numpy as np
import torch
root=Path('/root/autodl-tmp/solo_conquest_score_v5');stage=Path(__file__).resolve().parent
sys.path.append(str(root/'tools/BattleRl'))
from solo_sequence_forest_policy import SoloSequenceForestPolicy as SoloForestPolicy
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from solo_policy_inputs import SoloPolicyInputs
from solo_episode_data import SoloEpisodeData,state_signature
from solo_ppo_update import SoloPPOUpdate

out=stage/'sequence_ppo_gate';out.mkdir(exist_ok=False)
torch.set_num_threads(1);torch.manual_seed(7112026)
schema=PublicSchema(root/'public_dto_schema.json');assets=SoloTextAssets(root/'text_assets',root)
builder=SoloPolicyInputs(schema,assets);model=SoloForestPolicy(schema,assets.catalog.signature).cuda()
collection=root/'cuda_runtime_gate/collection'
model.load_state_dict(torch.load(collection/'behavior_policy.pt',weights_only=True,map_location='cuda'))
episodes=[SoloEpisodeData(collection/f'episode_{i:06d}/result.json') for i in range(7)]
before=state_signature(model.state_dict());updater=SoloPPOUpdate(model,device='cuda')
started=time.perf_counter()
def progress(*_):
    (out/'progress.json').write_text(json.dumps(dict(accepted_steps=updater.policy.accepted_steps,elapsed=time.perf_counter()-started)))
updater.policy.optimizer.register_step_post_hook(progress)
result=updater.update_complete(episodes,builder,7112026,progress_path=out/'optimizer_progress.json')
(out/'optimizer.json').write_text(json.dumps(result,indent=2))
after=state_signature(model.state_dict());assert before!=after and result['accepted_optimizer_steps']>0
torch.save(dict(network=model.state_dict(),optimizer=updater.policy.optimizer.state_dict(),torch_rng=torch.get_rng_state(),cuda_rng=torch.cuda.get_rng_state_all()),out/'checkpoint.pt')
restored=SoloForestPolicy(schema,assets.catalog.signature).cuda()
restored.load_state_dict(torch.load(out/'checkpoint.pt',weights_only=False,map_location='cuda')['network'])
assert state_signature(restored.state_dict())==after
predictions=[]
with torch.inference_mode():
    for episode in episodes:
        v,l,_,_=model.evaluate_sequence(episode.prepared_steps(builder),episode.actions)
        if not predictions:
            rv,rl,_,_=restored.evaluate_sequence(episode.prepared_steps(builder),episode.actions)
            torch.testing.assert_close(v,rv,rtol=1e-5,atol=1e-6);torch.testing.assert_close(l,rl,rtol=1e-5,atol=1e-6)
        predictions.append(v.cpu().numpy())
target=np.concatenate([e.returns for e in episodes]);pre=np.concatenate([e.values for e in episodes]);post=np.concatenate(predictions)
ev=lambda p:None if np.ptp(target)==0 else float(1-np.var(p-target)/np.var(target))
old_checkpoint=torch.load(root/'cuda_runtime_gate/checkpoint.pt',weights_only=False,map_location='cpu')
old=old_checkpoint['network']
new_adam=updater.policy.optimizer.state_dict()
old_adam=old_checkpoint['optimizer']
assert new_adam['param_groups']==old_adam['param_groups']
assert new_adam['state'].keys()==old_adam['state'].keys()
adam_numerator=adam_denominator=0.
for key, row in new_adam['state'].items():
    assert row.keys()==old_adam['state'][key].keys()
    for field, value in row.items():
        other=old_adam['state'][key][field]
        if field=='step':assert value.item()==other.item()
        elif torch.is_tensor(value):
            x=value.detach().cpu().double();y=other.detach().cpu().double()
            adam_numerator+=float((x-y).square().sum());adam_denominator+=float(y.square().sum())
        else:assert value==other
numerator=denominator=0.
for name,p in model.state_dict().items():
    if torch.is_tensor(p):
        x=p.cpu().double();y=old[name].double();numerator+=float((x-y).square().sum());denominator+=float(y.square().sum())
report=dict(passed=True,complete_scenarios=7,decisions=len(target),accepted_optimizer_steps=result['accepted_optimizer_steps'],
    ppo_seconds=result['optimization_seconds'],old_ppo_seconds=json.loads((root/'cuda_runtime_gate/result.json').read_text())['ppo_seconds'],
    parameter_before=before,parameter_after=after,reload_parameter_exact=True,pre_ev=ev(pre),post_ev=ev(post),
    pre_mse=float(np.mean((pre-target)**2)),post_mse=float(np.mean((post-target)**2)),
    old_new_final_parameter_relative_l2=(numerator/max(denominator,1e-30))**.5,
    old_new_adam_relative_l2=(adam_numerator/max(adam_denominator,1e-30))**.5,
    scope='Seven same real on-policy scenarios; concurrent formal training/evaluation makes speed non-isolated. Not full128 speed or learning acceptance.')
old_result=json.loads((root/'cuda_runtime_gate/result.json').read_text())
report['passed']=bool(np.isfinite(post).all() and all(torch.isfinite(p).all() for p in model.parameters())
    and result['accepted_optimizer_steps']==old_result['accepted_optimizer_steps']
    and report['old_new_final_parameter_relative_l2']<1e-4
    and report['old_new_adam_relative_l2']<1e-4)
(out/'result.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
if not report['passed']:raise RuntimeError('Same-batch PPO fidelity gate failed; preserve evidence')
