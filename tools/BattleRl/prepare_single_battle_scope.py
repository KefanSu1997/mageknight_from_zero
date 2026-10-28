"""Restrict existing task definitions to one group; preserve original evidence."""
import copy
import json
from pathlib import Path
import numpy as np
from mixed_task_pool import validate_pool
from recurrent_checkpoint import load_checkpoint, save_checkpoint
from task_feasibility import file_hash


def main():
    parent=Path('AutomationOutputs/BattleRl/20261011_parallel8_updates100/checkpoint.npz')
    source=Path('AutomationOutputs/BattleRl/20261008_world_preparation_bridge/pool.json')
    out=Path('AutomationOutputs/BattleRl/20261011_single_battle_scope')
    out.mkdir(exist_ok=False)
    pool=json.loads(source.read_text(encoding='utf-8'))
    dll='tmp/battle_rl_original_world_bound_consumers_v2/BattleRl.dll'
    validate_pool(pool,dll)
    narrowed=copy.deepcopy(pool)
    excluded=[]
    for key in ('tasks','unknown_tasks'):
        narrowed[key]=[r for r in pool[key] if len(r['task']['definition']['EnemyGroups'])==1]
        excluded.extend(r['task_id'] for r in pool[key] if len(r['task']['definition']['EnemyGroups'])!=1)
    narrowed['scope']='Single declared enemy group only; native complete sequence now means one battle and its formal cleanup. Original tasks and certificates unchanged.'
    narrowed['scope_parent_sha256']=file_hash(source)
    narrowed['deferred_multi_battle_task_ids']=excluded
    validate_pool(narrowed,dll)
    (out/'pool.json').write_text(json.dumps(narrowed,ensure_ascii=False,indent=2),encoding='utf-8')
    with np.load(parent,allow_pickle=False) as arrays:header=json.loads(str(arrays['header']))
    assert header['metadata']['pool_sha256']==file_hash(source)
    assert header['metadata']['rules_sha256']==file_hash(dll)
    parameters,opt,rng,replay=load_checkpoint(parent,header['metadata'])
    assert replay['at_native_episode_boundary']
    ids={r['task_id'] for k in ('tasks','unknown_tasks') for r in narrowed[k]}
    sampler=replay['sampler']
    (out/'parent_sampler_history.json').write_text(json.dumps(sampler,ensure_ascii=False,indent=2),encoding='utf-8')
    sampler['task_ids']=sorted(ids)
    sampler['attempts']={k:v for k,v in sampler['attempts'].items() if k in ids}
    # Events remain historical pretraining evidence, never relabelled as new
    # single-battle training. The complete old sampler is archived above.
    metadata=copy.deepcopy(header['metadata']);metadata['pool_sha256']=file_hash(out/'pool.json')
    save_checkpoint(out/'checkpoint.npz',opt,rng,metadata,replay)
    with np.load(parent,allow_pickle=False) as old,np.load(out/'checkpoint.npz',allow_pickle=False) as new:
        keys=[k for k in old.files if k!='header']
        assert all(np.array_equal(old[k],new[k]) for k in keys)
    report=dict(certified=len(narrowed['tasks']),unknown=len(narrowed['unknown_tasks']),
        heroes=sorted({r['hero'] for k in ('tasks','unknown_tasks') for r in narrowed[k]}),
        deferred=len(excluded),unchanged_arrays=len(keys),optimizer_steps=opt.steps,
        checkpoint_parent=str(parent),parent_sha256=file_hash(parent),
        rng_unchanged=rng.bit_generator.state==header['random_state'],
        prior_events_scope='Historical mixed-task pretraining, not single-battle acceptance')
    (out/'migration.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps(report))

if __name__=='__main__':main()
