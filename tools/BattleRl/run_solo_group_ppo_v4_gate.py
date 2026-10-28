"""Isolated seven-complete-scenario CUDA update; waits for live formal update boundary."""
import json,os,sys,time
from pathlib import Path
import numpy as np
import torch

root=Path('/root/autodl-tmp/solo_conquest_adventure_v4')
sys.path.append(str(root/'tools/BattleRl'))
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from solo_policy_inputs import SoloPolicyInputs
from solo_episode_data import SoloEpisodeData,state_signature
from solo_group_prior_policy import SoloGroupPriorPolicy
from solo_ppo_update import SoloPPOUpdate

out=Path('/root/solo_group_prior_adventure_v4_ppo_gate_20261022')
out.mkdir(exist_ok=False)
formal=Path('/root/mageknight_solo_conquest_A_forest100_20261022')
proc=Path('/proc/742764')
def identity():
    try:return (proc/'cmdline').read_bytes(),(proc/'stat').read_text().split(') ',1)[1].split()[19]
    except FileNotFoundError:return None
initial=identity()
if initial is not None:
    assert b'train_solo_conquest.py' in initial[0] and b'solo_conquest_A_forest_v3' in initial[0]
while not (formal/'optimizer_0001.json').exists() and initial is not None and identity()==initial:
    (out/'queue_status.json').write_text(json.dumps(dict(pid=os.getpid(),state='waiting_formal_first_optimizer',time=time.time())))
    time.sleep(20)
collection=Path('/root/solo_group_prior_adventure_v4_gate_20261022/collection')
assert json.loads((collection.parent/'result.json').read_text())['passed']
(out/'queue_status.json').write_text(json.dumps(dict(pid=os.getpid(),state='running_isolated_ppo_gate',time=time.time())))
torch.set_num_threads(1);torch.manual_seed(7112026)
schema=PublicSchema(root/'public_dto_schema.json');assets=SoloTextAssets(root/'text_assets',root)
builder=SoloPolicyInputs(schema,assets);model=SoloGroupPriorPolicy(schema,assets.catalog.signature).cuda()
model.load_state_dict(torch.load(collection/'behavior_policy.pt',weights_only=True,map_location='cuda'))
episodes=[SoloEpisodeData(collection/f'episode_{i:06d}/result.json') for i in range(7)]
before=state_signature(model.state_dict());updater=SoloPPOUpdate(model,device='cuda')
result=updater.update_complete(episodes,builder,7112026,progress_path=out/'optimizer_progress.json')
(out/'optimizer.json').write_text(json.dumps(result,indent=2))
after=state_signature(model.state_dict())
assert before!=after and result['accepted_optimizer_steps']>0
assert all(torch.isfinite(p).all() for p in model.parameters())
assert all(torch.isfinite(v).all() for row in updater.policy.optimizer.state.values() for v in row.values() if torch.is_tensor(v))
torch.save(dict(network=model.state_dict(),optimizer=updater.policy.optimizer.state_dict(),
                torch_rng=torch.get_rng_state(),cuda_rng=torch.cuda.get_rng_state_all()),out/'checkpoint.pt')
restored=SoloGroupPriorPolicy(schema,assets.catalog.signature).cuda()
restored.load_state_dict(torch.load(out/'checkpoint.pt',weights_only=False,map_location='cuda')['network'])
assert state_signature(restored.state_dict())==after
predictions=[]
with torch.inference_mode():
    for i,episode in enumerate(episodes):
        v,l,_,_=model.evaluate_sequence(episode.prepared_steps(builder),episode.actions)
        if i==0:
            rv,rl,_,_=restored.evaluate_sequence(episode.prepared_steps(builder),episode.actions)
            torch.testing.assert_close(v,rv,rtol=1e-5,atol=1e-6);torch.testing.assert_close(l,rl,rtol=1e-5,atol=1e-6)
        predictions.append(v.cpu().numpy())
target=np.concatenate([e.returns for e in episodes]);pre=np.concatenate([e.values for e in episodes]);post=np.concatenate(predictions)
assert np.isfinite(post).all()
ev=lambda p:None if np.ptp(target)==0 else float(1-np.var(p-target)/np.var(target))
report=dict(passed=True,complete_scenarios=7,decisions=len(target),parameter_before=before,parameter_after=after,
            accepted_optimizer_steps=result['accepted_optimizer_steps'],ppo_seconds=result['optimization_seconds'],
            pre_ev=ev(pre),post_ev=ev(post),pre_mse=float(np.mean((pre-target)**2)),post_mse=float(np.mean((post-target)**2)),
            reload_parameter_exact=True,scope='Seven training scenarios CUDA integration only; not formal128 speed or EV learning acceptance')
(out/'result.json').write_text(json.dumps(report,indent=2));print(json.dumps(report),flush=True)
