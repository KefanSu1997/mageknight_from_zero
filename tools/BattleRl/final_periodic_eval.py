"""Same fixed tasks/RNG/denominator; bounded parallel native creation/restoration."""
import json
from concurrent.futures import ThreadPoolExecutor
import numpy as np
import train_sb3_atomic_entity_text as base
import parallel_hierarchical_eval
from final_token_policy import BattleEnv
from sb3_battle_env import SharedCatalogRules

def frozen_evaluation(model, rows, history):
    output=model.output/f'eval_{model.completed_updates:04d}'
    output.mkdir(exist_ok=False)
    rng=base.rng_state();before=base.parameter_sha(model.policy);was_training=model.policy.training
    envs=getattr(getattr(model,'env',None),'envs',[])
    snapshots=[e.snapshot() if e.current is not None and hasattr(e,'row') else None for e in envs]
    for e in envs:e.rules.close()
    try:
        model.policy.set_training_mode(False)
        parallel_hierarchical_eval.BattleEnv=BattleEnv
        records=parallel_hierarchical_eval.collect(model,rows,history,output,workers=32)
    finally:
        def restore(pair):
            e,s=pair;e.rules=SharedCatalogRules()
            if s is not None:e.restore(s)
        try:
            with ThreadPoolExecutor(max_workers=4) as pool:list(pool.map(restore,zip(envs,snapshots)))
        finally:
            base.restore_rng(rng);model.policy.set_training_mode(was_training)
    assert base.parameter_sha(model.policy)==before
    result=dict(update=model.completed_updates,episodes=len(records),wins=sum(r['won'] for r in records),
        performance=base.performance_metrics([r['performance'] for r in records]),mean_reward=float(np.mean([r['reward'] for r in records])),
        frozen_parameters=True,training_rng_restored=True,per_task_actor_seed=True,records=records)
    (output/'summary.json').write_text(json.dumps(result,indent=2))
    with (model.output/'evaluation_metrics.jsonl').open('a') as f:f.write(json.dumps({k:v for k,v in result.items() if k!='records'})+'\n')
    base.render(model.output,f'固定评估 update {model.completed_updates} 完成')

