"""Fresh baseline-A recurrent PPO on complete solo-conquest games."""
import argparse,hashlib,json,os,time,shutil,sys
from pathlib import Path
if __name__=='__main__' and '--repo' in sys.argv:os.chdir(sys.argv[sys.argv.index('--repo')+1])
os.environ.update(OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',SOLO_BASELINE_A_OBSERVATION='1',BATTLE_RL_ENCODER_BATCH='4')
import numpy as np
import torch
from baseline_a_scenario_inputs import ScenarioInputs
from baseline_a_scenario_runtime import ScenarioRuntime
from baseline_a_scenario_text import ScenarioText
from baseline_a_scenario_behavior import ScenarioBehavior
from baseline_a_scenario_ppo import fresh_model,update_complete
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from solo_native_env import SoloNativeEnv
from solo_native_pool import SoloNativePool
from solo_episode_collector import collect_scenarios
from solo_episode_data import SoloEpisodeData
from solo_rollout_review import review_episodes
from solo_rollout_metrics import completed_update_record
from solo_training_diagnostics import inspect_updates
from solo_world_manifest import read_tasks
from train_solo_conquest import write_json

def sha(p):
    h=hashlib.sha256()
    with Path(p).open('rb') as f:
        for b in iter(lambda:f.read(8388608),b''):h.update(b)
    return h.hexdigest()

def setup(repo,entity_assets,out):
    repo=Path(repo);entity_assets=Path(entity_assets);out=Path(out)
    schema=PublicSchema(repo/'public_dto_schema.json');assets=SoloTextAssets(repo/'text_assets',repo)
    meta=json.loads((entity_assets/'manifest.json').read_text());vectors_file=entity_assets/'entity_vectors.npy'
    assert sha(vectors_file)==meta['assets_sha256']['entity_vectors.npy']
    vectors=np.concatenate((np.load(vectors_file),np.zeros((1,384),np.float32)))
    inputs=ScenarioInputs(repo,schema,assets,meta['entity_ids'])
    sources={p.name:sha(p) for p in Path(__file__).parent.glob('baseline_a_*.py')}
    binding=dict(sources=sources,schema=schema.signature,catalog=assets.catalog.signature,
                 entity_sha256=sha(vectors_file))
    runtime=ScenarioRuntime(out/'contexts',binding)
    return inputs,vectors,runtime,ScenarioText(schema,assets),binding

def checkpoint(model,out,number,rng,contract):
    start=time.perf_counter();path=out/f'checkpoint_{number:04d}.zip'
    model.save(path)
    state=out/f'continuation_{number:04d}.pt'
    torch.save(dict(completed_updates=number,task_rng=rng.bit_generator.state,
        numpy_rng=np.random.get_state(),torch_rng=torch.get_rng_state(),cuda_rng=torch.cuda.get_rng_state_all(),
        contract=contract),state)
    receipt=dict(update=number,path=str(path),sha256=sha(path),continuation=str(state),
        continuation_sha256=sha(state),seconds=time.perf_counter()-start)
    write_json(out/f'checkpoint_{number:04d}.json',receipt)
    return receipt

def run(a):
    assert os.name!='nt' and torch.cuda.is_available()
    torch.set_num_threads(1);torch.manual_seed(a.seed)
    out=Path(a.out);out.mkdir(parents=True,exist_ok=False)
    train=read_tasks(a.worlds,'train');valid=read_tasks(a.worlds,'validation')
    world_index=json.loads((Path(a.worlds)/'index.json').read_text())
    assert (len(train),len(valid))==(7000,700)
    assert world_index['files']['test']['worlds']==100 and world_index['files']['test']['episodes']==700
    assert not ({t['id'] for t in train}&{t['id'] for t in valid})
    assert not ({t['family_sha256'] for t in train}&{t['family_sha256'] for t in valid})
    with SoloNativeEnv(Path(a.repo)/'native/SoloConquestRl.dll',a.repo,out/'preflight') as env:
        native_binding=env.capabilities['binding_sha256'];admission=env.admission['sha256']
    if shutil.disk_usage(out).free<4*2**30:raise RuntimeError('4GiB formal entry reserve')
    inputs,vectors,runtime,renderer,binding=setup(a.repo,a.entity_assets,out)
    try:
        model=fresh_model(inputs,vectors,seed=a.seed);behavior=ScenarioBehavior(model.policy,inputs,runtime,renderer)
        contract=dict(input_binding=binding,native_binding=native_binding,admission_sha256=admission,
            world_index_sha256=sha(Path(a.worlds)/'index.json'),workers=32,episodes_per_update=128,
            batch_size=128,epochs=4,gamma=1,gae_lambda=1,seed=a.seed,updates=a.updates,
            reward='native solo final-score ledger',training_source_sha256=sha(__file__),test_locked=True)
        write_json(out/'contract.json',contract);rng=np.random.default_rng(a.seed);metrics=[]
        saved=checkpoint(model,out,0,rng,contract)
        write_json(out/'evaluation_requests/0000.json',dict(checkpoint=saved,split='validation',episodes=700))
        with SoloNativePool(Path(a.repo)/'native/SoloConquestRl.dll',a.repo,out/'workers',workers=32) as pool:
            for number in range(1,a.updates+1):
                if shutil.disk_usage(out).free<3.5*2**30:
                    checkpoint(model,out,number-1,rng,contract)
                    write_json(out/'storage_stop.json',dict(completed_updates=number-1));return
                assert {p.name:sha(p) for p in Path(__file__).parent.glob('baseline_a_*.py')}==binding['sources']
                start=time.perf_counter();selected=[]
                for i in rng.choice(len(train),128,replace=False):
                    task=dict(train[int(i)]);task['actor_seed']=int(rng.integers(0,2**63-1));selected.append(task)
                rollout=out/f'rollout_{number:04d}'
                # Independent immutable journals per completed update can be
                # archived without repeatedly copying every earlier context.
                next_runtime=ScenarioRuntime(rollout/'contexts',binding)
                if runtime is not None:runtime.close()
                runtime=next_runtime
                behavior=ScenarioBehavior(model.policy,inputs,runtime,renderer)
                write_json(out/'phase.json',dict(pid=os.getpid(),update=number,phase='collection',started_unix=time.time()))
                model.policy.set_training_mode(False)
                collection=collect_scenarios(pool,behavior,selected,rollout,ready_batch=True)
                assert collection['complete']==128
                episodes=[SoloEpisodeData(rollout/f'episode_{i:06d}/result.json') for i in range(128)]
                review=review_episodes(episodes);write_json(out/f'trajectory_review_{number:04d}.json',review)
                if review['status']=='review_required':raise RuntimeError('Trajectory cycle requires investigation before PPO')
                rows=[]
                for i in range(128):
                    data=torch.load(rollout/f'episode_{i:06d}/behavior_context.pt',weights_only=False,map_location='cpu')
                    assert data['pending'] is None and data['rows'][-1]['terminated'];rows.append(data['rows'])
                if shutil.disk_usage(out).free<3*2**30:raise RuntimeError('Preserved complete rollout; disk guard before PPO')
                write_json(out/'phase.json',dict(pid=os.getpid(),update=number,phase='ppo',started_unix=time.time()))
                optimized=update_complete(model,rows,runtime);optimized['optimization_seconds']=optimized['seconds']
                write_json(out/f'optimizer_{number:04d}.json',optimized)
                saving=time.perf_counter();saved=None
                if number%10==0 or number==a.updates:saved=checkpoint(model,out,number,rng,contract)
                record=completed_update_record(episodes,optimized,run_id=out.name,update=number,rollout=rollout,
                    timing_seconds=dict(collection=collection['wall_seconds'],ppo=optimized['seconds'],
                        save=time.perf_counter()-saving,archive=sum(e.result['archive']['save_wall_seconds'] for e in episodes),
                        full_update_wall=time.perf_counter()-start))
                metrics.append(record);write_json(out/'metrics.json',metrics)
                runtime.close();runtime=None
                write_json(rollout/'closed_for_archive.json',dict(update=number,complete_episodes=128,optimizer_complete=True))
                diagnostics=inspect_updates(metrics);write_json(out/'diagnostics.json',diagnostics)
                if saved and (number%20==0 or number==a.updates):
                    write_json(out/f'evaluation_requests/{number:04d}.json',dict(checkpoint=saved,split='validation',episodes=700))
                print(json.dumps(record),flush=True)
                if diagnostics['status']=='review_required':
                    if saved is None:checkpoint(model,out,number,rng,contract)
                    write_json(out/'review_required.json',diagnostics);return
        write_json(out/'training_complete.json',dict(completed_updates=a.updates,fixed_evaluations_complete=False))
    finally:
        if runtime is not None:runtime.close()

if __name__=='__main__':
    p=argparse.ArgumentParser()
    for key in ('repo','entity-assets','worlds','out'):p.add_argument('--'+key,required=True)
    p.add_argument('--updates',type=int,default=100);p.add_argument('--seed',type=int,default=7112026)
    args=p.parse_args();os.chdir(args.repo);run(args)
