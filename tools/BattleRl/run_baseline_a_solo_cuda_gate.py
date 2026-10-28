"""Independent actual A/full-game CUDA acceptance; never auto-start formal work."""
import argparse,hashlib,json,os,shutil,time,traceback
from pathlib import Path
os.environ.update(OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',SOLO_BASELINE_A_OBSERVATION='1',BATTLE_RL_ENCODER_BATCH='4')
import numpy as np
import torch
from baseline_a_scenario_inputs import ScenarioInputs
from baseline_a_scenario_text import ScenarioText
from baseline_a_scenario_runtime import ScenarioRuntime
from baseline_a_scenario_behavior import ScenarioBehavior
from baseline_a_scenario_ppo import fresh_model,update_complete
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from solo_native_pool import SoloNativePool
from solo_episode_collector import collect_scenarios
from solo_world_manifest import read_tasks


def signature(model):
    h=hashlib.sha256()
    for name,p in sorted(model.state_dict().items()):h.update(name.encode());h.update(p.detach().cpu().contiguous().numpy().tobytes())
    return h.hexdigest()


def run(a):
    if os.name=='nt' or not torch.cuda.is_available():raise RuntimeError('Remote CUDA gate only')
    out=Path(a.out);out.mkdir(exist_ok=False,parents=True)
    if shutil.disk_usage(out).free<3.25*2**30:raise RuntimeError('3.25GiB gate entry protection')
    torch.set_num_threads(1);torch.manual_seed(7112026)
    repo=Path(a.repo);os.chdir(repo)
    schema=PublicSchema(repo/'public_dto_schema.json');assets=SoloTextAssets(repo/'text_assets',repo)
    entity_assets=Path(a.entity_assets);meta=json.loads((entity_assets/'manifest.json').read_text())
    raw=entity_assets/'entity_vectors.npy'
    if hashlib.sha256(raw.read_bytes()).hexdigest()!=meta['assets_sha256']['entity_vectors.npy']:raise ValueError('A entity cache mismatch')
    vectors=np.concatenate((np.load(raw),np.zeros((1,384),np.float32)))
    inputs=ScenarioInputs(repo,schema,assets,meta['entity_ids']);model=fresh_model(inputs,vectors)
    sources={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in Path(__file__).parent.glob('baseline_a_*.py')}
    runtime=ScenarioRuntime(out/'contexts',dict(sources=sources,schema=schema.signature,catalog=assets.catalog.signature,
        entity_sha256=meta['assets_sha256']['entity_vectors.npy']))
    try:
        behavior=ScenarioBehavior(model.policy,inputs,runtime,ScenarioText(schema,assets))
        model.save(out/'initial_checkpoint.zip');before=signature(model.policy)
        tasks=read_tasks(a.worlds,'train')[:7]
        (out/'contract.json').write_text(json.dumps(dict(gate_only=True,tasks=tasks,sources=sources,
            batch128=True,epochs=4,workers=3,actual_frozen_reader=True),indent=2))
        started=time.perf_counter()
        with SoloNativePool(repo/'native/SoloConquestRl.dll',repo,out/'workers',workers=3,preparation=True) as pool:
            report=collect_scenarios(pool,behavior,tasks,out/'collection',ready_batch=True)
        if report['complete']!=7:raise ValueError('Not all seven complete games passed audit')
        episodes=[]
        for i in range(7):
            saved=torch.load(out/f'collection/episode_{i:06d}/behavior_context.pt',weights_only=False,map_location='cpu')
            if saved['pending'] is not None or not saved['rows'][-1]['terminated']:raise ValueError('Incomplete behavior archive')
            episodes.append(saved['rows'])
        if signature(model.policy)!=before:raise ValueError('Behavior changed policy')
        if shutil.disk_usage(out).free<3*2**30:raise RuntimeError('3GiB protection before PPO')
        result=update_complete(model,episodes,runtime);after=signature(model.policy)
        if before==after:raise ValueError('No actual parameter change')
        if runtime.reader is None or any(p.requires_grad or p.grad is not None for p in runtime.reader.model.parameters()):
            raise ValueError('Encoder freeze contract failed')
        model.save(out/'checkpoint_0001.zip')
        from sb3_contrib import RecurrentPPO
        restored=RecurrentPPO.load(out/'checkpoint_0001.zip',device='cuda')
        if signature(restored.policy)!=after:raise ValueError('Checkpoint reload changed weights')
        restored.policy.bind_runtime(runtime)
        (out/'result.json').write_text(json.dumps(dict(passed=True,collection=report,update=result,
            before_sha256=before,after_sha256=after,reload_exact=True,wall_seconds=time.perf_counter()-started,
            scope='Seven complete training tasks, real frozen joint text and one CUDA update; not formal128 throughput or EV learning acceptance'),indent=2))
    finally:runtime.close()

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--repo',required=True);p.add_argument('--out',required=True)
    p.add_argument('--worlds',required=True);p.add_argument('--entity-assets',required=True);a=p.parse_args()
    try:run(a)
    except BaseException:
        Path(a.out).mkdir(parents=True,exist_ok=True)
        (Path(a.out)/'failure.txt').write_text(traceback.format_exc());raise
