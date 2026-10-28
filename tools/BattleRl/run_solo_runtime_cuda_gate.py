"""Seven real scenario CUDA integration, not a 128-episode formal update."""
import json
from pathlib import Path
import sys
import time
import torch

root=Path(sys.argv[1]).resolve();sys.path.insert(0,str(root/'tools/BattleRl'))
from solo_native_pool import SoloNativePool
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from solo_policy_inputs import SoloPolicyInputs
from solo_structured_policy import SoloStructuredPolicy
from solo_forest_policy import SoloForestPolicy
from solo_episode_collector import SoloBehavior,collect_scenarios
from solo_episode_data import SoloEpisodeData,state_signature
from solo_ppo_update import SoloPPOUpdate
from solo_rollout_review import review_episodes

out=root/'cuda_runtime_gate';out.mkdir(exist_ok=False)
assert torch.cuda.is_available()
torch.set_num_threads(1);torch.manual_seed(7112026)
schema=PublicSchema(root/'public_dto_schema.json');assets=SoloTextAssets(root/'text_assets',root)
policy_class=SoloForestPolicy if '--forest' in sys.argv[2:] else SoloStructuredPolicy
builder=SoloPolicyInputs(schema,assets);model=policy_class(schema,assets.catalog.signature).cuda()
tasks=[dict(t,id='cuda-gate/'+t['setup']['Hero']) for t in json.loads((root/'gate_family.json').read_text())['tasks']]
start=time.perf_counter()
with SoloNativePool(root/'native/SoloConquestRl.dll',root,out/'workers',workers=3,preparation=True) as pool:
    collection=collect_scenarios(pool,SoloBehavior(model,builder),tasks,out/'collection',max_decisions=512)
if collection['complete']!=7:raise RuntimeError('Full seven-scenario collection failed; preserve all results')
episodes=[SoloEpisodeData(out/f'collection/episode_{i:06d}/result.json') for i in range(7)]
review=review_episodes(episodes);(out/'trajectory_review.json').write_text(json.dumps(review,indent=2))
if review['status']=='review_required':raise RuntimeError('Cycle candidates require replay before optimization')
before=state_signature(model.state_dict());updater=SoloPPOUpdate(model,device='cuda')
result=updater.update_complete(episodes,builder,7112026,progress_path=out/'optimizer_progress.json')
(out/'optimizer.json').write_text(json.dumps(result,indent=2))
after=state_signature(model.state_dict())
assert after!=before and result['accepted_optimizer_steps']>0
assert all(torch.isfinite(p).all() for p in model.parameters())
torch.save(dict(network=model.state_dict(),optimizer=updater.policy.optimizer.state_dict(),
    torch_rng=torch.get_rng_state(),cuda_rng=torch.cuda.get_rng_state_all()),out/'checkpoint.pt')
restored=policy_class(schema,assets.catalog.signature).cuda()
restored.load_state_dict(torch.load(out/'checkpoint.pt',map_location='cuda',weights_only=False)['network'])
assert state_signature(restored.state_dict())==after
with torch.inference_mode():
    v,l,_,_=model.evaluate_sequence(episodes[0].prepared_steps(builder),episodes[0].actions)
    rv,rl,_,_=restored.evaluate_sequence(episodes[0].prepared_steps(builder),episodes[0].actions)
torch.testing.assert_close(v,rv,rtol=1e-5,atol=1e-6)
torch.testing.assert_close(l,rl,rtol=1e-5,atol=1e-6)
report=dict(passed=True,policy_class=policy_class.__name__,complete_scenarios=7,collection_seconds=collection['wall_seconds'],
    ppo_seconds=result['optimization_seconds'],full_gate_seconds=time.perf_counter()-start,
    accepted_optimizer_steps=result['accepted_optimizer_steps'],parameter_before=before,parameter_after=after,
    reload_parameter_exact=True,reload_value_max_difference=float((v-rv).abs().max()),
    reload_logp_max_difference=float((l-rl).abs().max()),formal_training_started=False)
(out/'result.json').write_text(json.dumps(report,indent=2));print(json.dumps(report),flush=True)
