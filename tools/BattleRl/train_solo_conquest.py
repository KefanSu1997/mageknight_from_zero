"""Fresh full-scenario A-family training and independent fixed-world evaluation.

Requires a formally admitted native adapter. No preparation bypass, synthetic
world fallback, truncated episodes, or automatic restart after an audit failure.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import time
import uuid
import numpy as np
import torch
from solo_native_env import SoloNativeEnv
from solo_native_pool import SoloNativePool
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from solo_policy_inputs import SoloPolicyInputs
from solo_structured_policy import SoloStructuredPolicy
from solo_forest_policy import SoloForestPolicy
from solo_episode_collector import SoloBehavior, collect_scenarios
from solo_episode_data import SoloEpisodeData
from solo_ppo_update import SoloPPOUpdate
from solo_rollout_metrics import completed_update_record
from solo_training_diagnostics import inspect_updates
from solo_world_manifest import read_tasks
from solo_rollout_review import review_episodes
from solo_progress import publish_progress
from solo_boundary_storage import protect_boundary


def write_json(path,value):
    path=Path(path);path.parent.mkdir(parents=True,exist_ok=True)
    pending=path.with_name(path.name+'.'+uuid.uuid4().hex+'.pending')
    with pending.open('x',encoding='utf-8') as stream:
        json.dump(value,stream,ensure_ascii=False,allow_nan=False,indent=2)
        stream.flush();os.fsync(stream.fileno())
    os.replace(pending,path)


def python_sources():
    folder=Path(__file__).resolve().parent
    paths=list(folder.glob('solo_*.py'))+[Path(__file__).resolve()]
    return {p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(paths)}


def checkpoint(out,update,network,updater,rng,contract):
    path=out/f'checkpoint_{update:04d}.pt'
    pending=path.with_name(path.name+'.'+uuid.uuid4().hex+'.pending')
    torch.save(dict(version=1,completed_updates=update,contract=contract,
        network=network.state_dict(),optimizer=updater.policy.optimizer.state_dict(),
        accepted_steps=updater.policy.accepted_steps,upstream_updates=updater._n_updates,
        task_rng=rng.bit_generator.state,torch_rng=torch.get_rng_state(),
        cuda_rng=torch.cuda.get_rng_state_all()),pending)
    os.replace(pending,path)
    receipt=dict(update=update,path=str(path.resolve()),sha256=hashlib.sha256(path.read_bytes()).hexdigest())
    write_json(path.with_suffix('.json'),receipt)
    return receipt


def restore_checkpoint(path,network,updater,rng,contract):
    path=Path(path)
    receipt=json.loads(path.with_suffix('.json').read_text(encoding='utf-8'))
    if hashlib.sha256(path.read_bytes()).hexdigest()!=receipt['sha256']:
        raise ValueError('Resume checkpoint SHA differs')
    saved=torch.load(path,map_location='cpu',weights_only=False)
    if saved['version']!=1 or saved['contract']!=contract or saved['completed_updates']!=receipt['update']:
        raise ValueError('Resume rules/input/configuration contract differs')
    network.load_state_dict(saved['network'])
    updater.policy.optimizer.load_state_dict(saved['optimizer'])
    updater.policy.accepted_steps=saved['accepted_steps'];updater._n_updates=saved['upstream_updates']
    rng.bit_generator.state=saved['task_rng']
    torch.set_rng_state(saved['torch_rng'])
    if len(saved['cuda_rng'])!=torch.cuda.device_count():
        raise ValueError('CUDA RNG device count changed; explicit migration required')
    torch.cuda.set_rng_state_all(saved['cuda_rng'])
    return saved['completed_updates'],receipt


def run(args):
    if os.name=='nt' or not torch.cuda.is_available():
        raise RuntimeError('Full RL optimization must run on the remote CUDA host')
    if args.updates<1:raise ValueError('Positive update budget required')
    torch.set_num_threads(1);torch.manual_seed(args.seed)
    tasks=read_tasks(args.worlds,'train')
    validation=read_tasks(args.worlds,'validation')
    if len(tasks)!=7000 or len(validation)!=700:raise ValueError('Unexpected official split size')
    schema=PublicSchema(args.schema);assets=SoloTextAssets(args.assets,args.repo)
    builder=SoloPolicyInputs(schema,assets)
    out=Path(args.out);out.mkdir(parents=True,exist_ok=False)
    # Before network allocation or collection, check authoritative native admission.
    with SoloNativeEnv(args.dll,args.repo,out/'preflight') as native:
        binding=native.capabilities['binding_sha256']
        admission_sha256=getattr(native,'admission',{}).get('sha256')
        if args.candidate_batching and not getattr(native,'admission',{}).get('record',{}).get('candidate_batching_passed'):
            raise ValueError('Candidate batching requires explicit CUDA/PPO admission')
    if shutil.disk_usage(out).free<3*1024**3:raise RuntimeError('3 GiB disk protection')
    contract=dict(native_binding=binding,schema_sha256=schema.signature,
        admission_sha256=admission_sha256,
        candidate_batching=args.candidate_batching,
        catalog_signature=assets.catalog.signature,
        python_sources=python_sources(),
        world_index_sha256=hashlib.sha256((Path(args.worlds)/'index.json').read_bytes()).hexdigest(),
        workers=32,episodes_per_update=128,minibatch=128,epochs=4,gamma=1.,gae_lambda=1.,seed=args.seed)
    write_json(out/'contract.json',contract)
    rng=np.random.default_rng(args.seed)
    network=(SoloForestPolicy if args.candidate_batching else SoloStructuredPolicy)(schema,assets.catalog.signature).cuda()
    updater=SoloPPOUpdate(network,device='cuda')
    metrics=[];completed=0
    if args.resume:
        completed,parent=restore_checkpoint(args.resume,network,updater,rng,contract)
        if completed>=args.updates:raise ValueError('Resume checkpoint already reaches the requested goal')
        prior=json.loads((Path(args.resume).parent/'metrics.json').read_text(encoding='utf-8')) if completed else []
        metrics=[dict(row,run_id=out.name) for row in prior if row['update']<=completed]
        if [r['update'] for r in metrics]!=list(range(1,completed+1)):
            raise ValueError('Missing completed-update metrics; recover evidence before continuation')
        write_json(out/'parent.json',dict(checkpoint=parent,parent_run=str(Path(args.resume).parent.resolve())))
        write_json(out/'metrics.json',metrics)
    else:
        initial=checkpoint(out,0,network,updater,rng,contract)
        write_json(out/'evaluation_requests/0000.json',dict(checkpoint=initial,split='validation',episodes=700))
    with SoloNativePool(args.dll,args.repo,out/'workers',workers=32) as pool:
        if any(e.capabilities['binding_sha256']!=binding for e in pool.envs):
            raise RuntimeError('Worker rules/data differ')
        for update in range(completed+1,args.updates+1):
            wall=time.perf_counter()
            if python_sources()!=contract['python_sources']:
                raise RuntimeError('Bound training sources changed; preserve current evidence and migrate explicitly')
            def save_boundary(number):
                path=out/f'checkpoint_{number:04d}.pt'
                receipt_path=path.with_suffix('.json')
                if path.exists() and receipt_path.exists():
                    receipt=json.loads(receipt_path.read_text(encoding='utf-8'))
                    if receipt['update']!=number or hashlib.sha256(path.read_bytes()).hexdigest()!=receipt['sha256']:
                        raise ValueError('Existing boundary checkpoint failed verification')
                    return receipt
                return checkpoint(out,number,network,updater,rng,contract)
            if protect_boundary(free_bytes=shutil.disk_usage(out).free,completed_updates=update-1,
                    save=save_boundary,publish=lambda record:write_json(out/'storage_stop.json',record)):
                publish_progress(out/'phase.json',dict(pid=os.getpid(),update=update-1,
                    phase='stopped_disk_protection',started_unix=time.time()))
                return
            selected=[]
            for index in rng.choice(len(tasks),size=128,replace=False):
                task=dict(tasks[int(index)])
                task['actor_seed']=int(rng.integers(0,2**63-1))
                selected.append(task)
            rollout=out/f'rollout_{update:04d}'
            publish_progress(out/'phase.json',dict(pid=os.getpid(),update=update,phase='collection',started_unix=time.time()))
            network.eval()
            collection=collect_scenarios(pool,SoloBehavior(network,builder),selected,rollout)
            if collection['complete']!=128:
                write_json(out/'review_required.json',dict(reason='incomplete_or_failed_native_scenarios',
                           update=update,rollout=str(rollout),complete=collection['complete']))
                raise RuntimeError('Preserved failed collection; no affected batch optimization')
            episodes=[SoloEpisodeData(rollout/f'episode_{i:06d}/result.json') for i in range(128)]
            trajectory_review=review_episodes(episodes)
            write_json(out/f'trajectory_review_{update:04d}.json',trajectory_review)
            if trajectory_review['status']=='review_required':
                write_json(out/'review_required.json',dict(reason='positive_public_cycle_requires_replay',
                    update=update,rollout=str(rollout),candidates=trajectory_review['positive_public_cycle_candidates']))
                raise RuntimeError('Review preserved cycle candidates before optimizing; not yet a proven exploit')
            publish_progress(out/'phase.json',dict(pid=os.getpid(),update=update,phase='ppo',started_unix=time.time()))
            optimized=updater.update_complete(episodes,builder,int(rng.integers(0,2**63-1)),
                                             progress_path=out/'optimizer_progress.json')
            write_json(out/f'optimizer_{update:04d}.json',optimized)
            save_start=time.perf_counter();saved=None
            if update%10==0 or update==args.updates:
                publish_progress(out/'phase.json',dict(pid=os.getpid(),update=update,phase='save',started_unix=time.time()))
                saved=checkpoint(out,update,network,updater,rng,contract)
            save_seconds=time.perf_counter()-save_start
            # Native journal serialization is inside collection; report its measured
            # subset separately, without adding it to collection again.
            archive_seconds=sum(e.result['archive']['save_wall_seconds'] for e in episodes)
            record=completed_update_record(episodes,optimized,run_id=out.name,update=update,rollout=rollout,
                timing_seconds=dict(collection=collection['wall_seconds'],ppo=optimized['optimization_seconds'],
                    save=save_seconds,archive=archive_seconds,full_update_wall=time.perf_counter()-wall))
            record['archive_scope']='sum_of_concurrent_worker_save_seconds_within_collection_not_wall_time; remote_to_H_transfer_not_included'
            metrics.append(record);write_json(out/'metrics.json',metrics)
            review=inspect_updates(metrics);write_json(out/'diagnostics.json',review)
            if saved and (update%20==0 or update==args.updates):
                write_json(out/f'evaluation_requests/{update:04d}.json',dict(checkpoint=saved,split='validation',episodes=700))
            print(json.dumps(dict(update=update,reward=record['reward'],pre_update_ev=record['pre_update_ev'],
                                  timing_seconds=record['timing_seconds'])),flush=True)
            if review['status']=='review_required':
                if saved is None:checkpoint(out,update,network,updater,rng,contract)
                write_json(out/'review_required.json',review)
                return
    write_json(out/'training_complete.json',dict(completed_updates=args.updates,
               fixed_evaluations_complete=False,reason='Independent evaluator must finish queued checkpoints'))


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    for name in ('repo','dll','worlds','schema','assets','out'):parser.add_argument('--'+name,required=True)
    parser.add_argument('--updates',type=int,default=100);parser.add_argument('--seed',type=int,default=7112026)
    parser.add_argument('--resume',help='Verified boundary checkpoint; writes a new continuation directory')
    parser.add_argument('--candidate-batching',action='store_true',help='Requires same-support CUDA/gradient/PPO evidence')
    run(parser.parse_args())
