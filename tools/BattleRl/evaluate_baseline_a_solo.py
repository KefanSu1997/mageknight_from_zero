"""A-policy fixed700 evaluation, separately scheduled from training."""
import argparse,json,os,time,shutil,sys
from pathlib import Path
if __name__=='__main__' and '--repo' in sys.argv:os.chdir(sys.argv[sys.argv.index('--repo')+1])
import torch
from sb3_contrib import RecurrentPPO
from train_baseline_a_solo import setup,sha
from baseline_a_scenario_behavior import ScenarioBehavior
from solo_native_pool import SoloNativePool
from solo_episode_collector import collect_scenarios
from solo_episode_data import SoloEpisodeData,state_signature
from solo_rollout_metrics import summarize_complete_episodes
from solo_training_diagnostics import inspect_evaluation
from solo_world_manifest import read_tasks
from train_solo_conquest import write_json

def run(a):
    assert os.name!='nt' and torch.cuda.is_available()
    torch.set_num_threads(1);root=Path(a.run);lock=root/'evaluation_worker.lock'
    with lock.open('x') as f:json.dump(dict(pid=os.getpid(),started=time.time()),f)
    try:
        tasks=read_tasks(a.worlds,'validation');assert len(tasks)==700
        while True:
            requests=sorted((root/'evaluation_requests').glob('*.json'))
            for p in requests:
                out=root/('eval_'+p.stem)
                if (out/'summary.json').exists():continue
                if shutil.disk_usage(root).free<3.5*2**30:
                    write_json(root/'evaluation_storage_wait.json',dict(time=time.time(),request=str(p)));break
                req=json.loads(p.read_text());assert req['split']=='validation' and req['episodes']==700
                out.mkdir(exist_ok=False);start=time.perf_counter();queue=max(0,time.time()-p.stat().st_mtime)
                ckpt=req['checkpoint'];assert sha(ckpt['path'])==ckpt['sha256']
                assert sha(ckpt['continuation'])==ckpt['continuation_sha256']
                saved=torch.load(ckpt['continuation'],weights_only=False,map_location='cpu');contract=saved['contract']
                assert saved['completed_updates']==ckpt['update']
                assert sha(Path(a.worlds)/'index.json')==contract['world_index_sha256']
                inputs,vectors,runtime,renderer,binding=setup(a.repo,a.entity_assets,out)
                try:
                    assert binding==contract['input_binding']
                    model=RecurrentPPO.load(ckpt['path'],device='cuda');before=state_signature(model.policy.state_dict())
                    behavior=ScenarioBehavior(model.policy,inputs,runtime,renderer)
                    with SoloNativePool(Path(a.repo)/'native/SoloConquestRl.dll',a.repo,out/'workers',workers=32) as pool:
                        assert all(e.capabilities['binding_sha256']==contract['native_binding'] for e in pool.envs)
                        collection=collect_scenarios(pool,behavior,tasks,out/'collection',ready_batch=True)
                    assert state_signature(model.policy.state_dict())==before
                    episodes=[SoloEpisodeData(out/f'collection/episode_{i:06d}/result.json')
                        for i,e in enumerate(collection['episodes']) if e['eligible_complete_episode']]
                    summary=dict(checkpoint=ckpt,split='validation',worlds=100,episodes=700,completed=len(episodes),
                        errors=700-len(episodes),denominator=700,evaluation_wall=time.perf_counter()-start,
                        evaluation_queue_wait=queue,parameters_unchanged=True,
                        completed_scenario_metrics=summarize_complete_episodes(episodes) if episodes else None,
                        conquest_successes=sum(e.records[-1]['Transition']['Score']['Cities']==2 for e in episodes),
                        error_task_ids=[e['task_id'] for e in collection['episodes'] if not e['eligible_complete_episode']])
                    summary['diagnostic']=inspect_evaluation(summary);write_json(out/'summary.json',summary)
                    print(json.dumps(summary),flush=True)
                finally:runtime.close()
            finished=all((root/('eval_'+p.stem)/'summary.json').exists() for p in requests)
            if finished and any((root/name).exists() for name in ('training_complete.json','review_required.json','storage_stop.json')):return
            time.sleep(10)
    finally:lock.unlink()
if __name__=='__main__':
    p=argparse.ArgumentParser()
    for key in ('repo','entity-assets','worlds','run'):p.add_argument('--'+key,required=True)
    a=p.parse_args();os.chdir(a.repo);run(a)
