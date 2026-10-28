"""Independent 32-environment fixed-700 evaluation; never occupies trainer loop."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import time
import torch
from solo_native_pool import SoloNativePool
from solo_public_schema import PublicSchema
from solo_text_assets import SoloTextAssets
from solo_policy_inputs import SoloPolicyInputs
from solo_structured_policy import SoloStructuredPolicy
from solo_forest_policy import SoloForestPolicy
from solo_episode_collector import SoloBehavior,collect_scenarios
from solo_episode_data import SoloEpisodeData,state_signature
from solo_rollout_metrics import summarize_complete_episodes
from solo_training_diagnostics import inspect_evaluation
from solo_world_manifest import read_tasks
from train_solo_conquest import write_json,python_sources


def run(args):
    if not torch.cuda.is_available():raise RuntimeError('Remote CUDA evaluation required')
    torch.set_num_threads(1)
    root=Path(args.run);requests=root/'evaluation_requests'
    schema=PublicSchema(args.schema);assets=SoloTextAssets(args.assets,args.repo)
    builder=SoloPolicyInputs(schema,assets)
    tasks=read_tasks(args.worlds,'validation')
    if len(tasks)!=700:raise ValueError('All 100 worlds and seven heroes required')
    for request_path in sorted(requests.glob('*.json')):
        request=json.loads(request_path.read_text(encoding='utf-8'))
        if request['split']!='validation' or request['episodes']!=700:raise ValueError('Unexpected request')
        output=root/('eval_'+request_path.stem)
        if (output/'summary.json').exists():continue
        # Existing partials stay intact; explicit recovery must choose how to finish.
        output.mkdir(exist_ok=False)
        start=time.perf_counter();queue_wait=max(0.,time.time()-request_path.stat().st_mtime)
        ckpt=request['checkpoint'];path=Path(ckpt['path'])
        if hashlib.sha256(path.read_bytes()).hexdigest()!=ckpt['sha256']:raise ValueError('Checkpoint changed')
        saved=torch.load(path,map_location='cpu',weights_only=False)
        contract=saved['contract']
        if contract['python_sources']!=python_sources():raise ValueError('Evaluation policy code differs from checkpoint')
        if (contract['schema_sha256']!=schema.signature or contract['catalog_signature']!=assets.catalog.signature
            or contract['world_index_sha256']!=hashlib.sha256((Path(args.worlds)/'index.json').read_bytes()).hexdigest()):
            raise ValueError('Evaluation input/world contract differs')
        if saved['completed_updates']!=ckpt['update']:raise ValueError('Checkpoint update mismatch')
        model=(SoloForestPolicy if contract.get('candidate_batching',False) else SoloStructuredPolicy)(schema,assets.catalog.signature).cuda()
        model.load_state_dict(saved['network']);model.eval()
        before=state_signature(model.state_dict())
        with SoloNativePool(args.dll,args.repo,output/'workers',workers=32) as pool:
            if any(e.capabilities['binding_sha256']!=contract['native_binding'] for e in pool.envs):
                raise ValueError('Evaluation native binding differs')
            collection=collect_scenarios(pool,SoloBehavior(model,builder),tasks,output/'collection')
        if state_signature(model.state_dict())!=before:raise RuntimeError('Evaluation changed model')
        episodes=[SoloEpisodeData(output/f'collection/episode_{i:06d}/result.json')
                  for i,e in enumerate(collection['episodes']) if e['eligible_complete_episode']]
        summary=dict(checkpoint=ckpt,split='validation',worlds=100,episodes=700,completed=len(episodes),
            errors=700-len(episodes),evaluation_wall=time.perf_counter()-start,evaluation_queue_wait=queue_wait,
            completed_scenario_metrics=summarize_complete_episodes(episodes) if episodes else None,
            conquest_successes=sum(e.records[-1]['Transition']['Score']['Cities']==2 for e in episodes),
            denominator=700,parameters_unchanged=True,
            error_task_ids=[e['task_id'] for e in collection['episodes'] if not e['eligible_complete_episode']])
        summary['diagnostic']=inspect_evaluation(summary)
        write_json(output/'summary.json',summary)
        print(json.dumps(dict(update=ckpt['update'],completed=len(episodes),errors=700-len(episodes),
                              evaluation_wall=summary['evaluation_wall'])),flush=True)


def dispatch(args):
    root=Path(args.run)
    lock=root/'evaluation_worker.lock'
    # Never take over an existing lock on timeout; identify its process first.
    with lock.open('x',encoding='utf-8') as stream:
        json.dump(dict(pid=os.getpid(),started=time.time()),stream)
    try:
        while True:
            run(args)
            requests=list((root/'evaluation_requests').glob('*.json'))
            finished=all((root/('eval_'+p.stem)/'summary.json').exists() for p in requests)
            stopped=(root/'training_complete.json').exists() or (root/'review_required.json').exists()
            if not args.watch or stopped and finished:return
            time.sleep(10)
    finally:
        lock.unlink()


if __name__=='__main__':
    parser=argparse.ArgumentParser()
    for name in ('repo','dll','worlds','schema','assets','run'):parser.add_argument('--'+name,required=True)
    parser.add_argument('--watch',action='store_true',help='Consume new requests independently while training runs')
    dispatch(parser.parse_args())
