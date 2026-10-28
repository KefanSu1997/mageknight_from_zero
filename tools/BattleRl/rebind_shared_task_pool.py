"""Rebind existing train tasks through actual new-rule resets and witness replay.

No teacher data, fake successes, definition changes, or history backfill.
"""
import argparse
import copy
import json
import os
from pathlib import Path
from mixed_task_pool import validate_pool
from task_feasibility import certify_witness, digest, file_hash, SEQUENCE_GOAL
from original_data_signature import data_signature


def main():
    parser=argparse.ArgumentParser()
    for key in ('old-pool','old-dll','new-dll','out'):parser.add_argument('--'+key,required=True)
    parser.add_argument('--native-shapeshift',action='store_true')
    parser.add_argument('--native-world',action='store_true')
    args=parser.parse_args();old=json.loads(Path(args.old_pool).read_text())
    original_rows=validate_pool(old,args.old_dll)
    out=Path(args.out)
    if out.exists():raise ValueError('Preserve rule migration evidence')
    out.mkdir(parents=True)
    os.environ.update(BATTLE_RL_DLL=str(Path(args.new_dll).resolve()),BATTLE_RL_SHARED_SKILLS='1',
                      BATTLE_RL_SHAPESHIFT='1' if args.native_shapeshift else '0',
                      BATTLE_RL_ORIGINAL_WORLD='1' if args.native_world else '0',
                      BATTLE_RL_NATIVE_PAYMENTS='1',BATTLE_RL_NATIVE_CONVERSIONS='1',
                      BATTLE_RL_NATIVE_ENERGY_TARGETS='1',BATTLE_RL_ORIGINAL_OPENINGS='1',
                      BATTLE_RL_PROTOCOL='--serve-journey-artifacts')
    from train_ppo import Rules
    rules=Rules();certified=[];unknown=[];mapping={};failed=[]
    signatures=data_signature()
    try:
        indices={a['Key']:i for i,a in enumerate(rules.actions)}
        for row in original_rows:
            current=rules.reset_task(row['task']['definition'])
            if current['done'] or not current['legal']:raise ValueError('Prior root no longer live/legal')
            initial=copy.deepcopy(current['state'])
            binding=copy.deepcopy(row['task'])
            binding.update(rules_sha256=file_hash(args.new_dll),feature_schema=rules.info['featureSchema'],
                           catalog_sha256=digest(rules.actions),initial_state_sha256=digest(initial))
            if row['status']=='solvable_witness':
                old_witness=json.loads(Path(row['evidence']['path']).read_text(encoding='utf-8-sig'))
                actual=[]
                for step,prior in enumerate(old_witness['trace'][:row['evidence']['operations']]):
                    # New shared public state is explicit. All prior rule fields
                    # must still match exactly; no per-field tolerance/guessing.
                    if not set(current['state'])-set(prior['before'])<={'sharedSkills','world'}:raise ValueError('Unexpected new state fields')
                    projection={k:v for k,v in current['state'].items() if k in prior['before']}
                    if projection!=prior['before']:raise ValueError(f'Old before projection differs at {step}')
                    index=indices.get(prior['action']['Key'])
                    if index is None or rules.actions[index]!=prior['action'] or index not in current['legal']:
                        raise ValueError(f'Old actual action no longer legal at {step}')
                    following=rules.step(index)
                    if not set(following['state'])-set(prior['after'])<={'sharedSkills','world'}:raise ValueError('Unexpected new after-state fields')
                    projection={k:v for k,v in following['state'].items() if k in prior['after']}
                    if projection!=prior['after'] or following['reward']!=prior['reward']:
                        raise ValueError(f'Old after/reward projection differs at {step}')
                    actual.append(dict(before=current['state'],action=rules.actions[index],after=following['state'],reward=following['reward']))
                    current=following
                witness=out/f'{row["task_id"]}_replayed_witness.json'
                witness.write_text(json.dumps(dict(seed=binding['seed'],task_definition=binding['definition'],
                    goal=SEQUENCE_GOAL,trace=actual,provenance='Actual new-rule replay; not teacher samples or PPO coverage'),ensure_ascii=False),encoding='utf-8')
                new=certify_witness(rules,witness,args.new_dll)
                certified.append(new)
            else:
                new=copy.deepcopy(row);new.update(task=binding,task_id=digest(binding),data_signature=signatures,
                    admission=dict(status='legal_reset_not_solvability',initial_state_sha256=digest(initial),
                                   legal_actions=len(current['legal'])))
                unknown.append(new)
            mapping[row['task_id']]=new['task_id']
            (out/'progress.json').write_text(json.dumps(dict(certified=len(certified),unknown=len(unknown),total=len(original_rows)),indent=2),encoding='utf-8')
            if data_signature()!=signatures:raise ValueError('Original data changed during rebinding')
        result=copy.deepcopy(old);result.update(tasks=certified,unknown_tasks=unknown)
        validate_pool(result,args.new_dll)
        (out/'pool.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
        (out/'task_id_mapping.json').write_text(json.dumps(mapping,indent=2),encoding='utf-8')
        print(json.dumps(dict(status='actual_new_rule_bound_pool_not_new_training',certified=len(certified),unknown=len(unknown))))
    except Exception as error:
        failure=dict(reason=repr(error),task_id=row['task_id'] if 'row' in locals() else None,
                     current=current if 'current' in locals() else None,partial_certified=len(certified),partial_unknown=len(unknown))
        (out/'failure.json').write_text(json.dumps(failure,ensure_ascii=False,indent=2),encoding='utf-8')
        raise
    finally:rules.close()


if __name__=='__main__':main()
