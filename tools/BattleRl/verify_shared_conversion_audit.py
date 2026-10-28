"""Preserved actual failure, original fee/cap counterpart and strict negatives."""
import copy
import importlib.util
import json
import os
from pathlib import Path
from task_feasibility import file_hash
from trajectory_audit import audit


def main():
    out=Path('AutomationOutputs/BattleRl/20261008_shared_conversion_audit_failure')
    result=out/'verification.json'
    if result.exists():raise ValueError('Preserve previous checks')
    failure=Path('AutomationOutputs/BattleRl/20261008_shared_argument_control_continuation_32/failure.json')
    saved=json.loads(failure.read_text());trace=saved['trace']
    spec=importlib.util.spec_from_file_location('old_conversion_audit',out/'previous_trajectory_audit.py')
    old=importlib.util.module_from_spec(spec);spec.loader.exec_module(old)
    original=old.audit(trace);assert any(e['step']==5 for e in original['errors'])
    os.environ.update(BATTLE_RL_DLL=str(Path('tmp/battle_rl_original_world_bound_consumers_v2/BattleRl.dll').resolve()),
        BATTLE_RL_PROTOCOL='--serve-journey-artifacts',BATTLE_RL_NATIVE_PAYMENTS='1',BATTLE_RL_NATIVE_CONVERSIONS='1',
        BATTLE_RL_NATIVE_ENERGY_TARGETS='1',BATTLE_RL_ORIGINAL_OPENINGS='1',BATTLE_RL_SHARED_SKILLS='1',BATTLE_RL_SHAPESHIFT='1',BATTLE_RL_ORIGINAL_WORLD='1')
    from train_ppo import Rules
    pool=json.loads(Path('AutomationOutputs/BattleRl/20261008_world_preparation_bridge/pool.json').read_text())
    definition=next(r['task']['definition'] for r in pool['tasks']+pool['unknown_tasks'] if r['task_id']==saved['task_id'])
    env=Rules();cases=[]
    try:
        current=env.reset_task(definition);keys={a['Key']:i for i,a in enumerate(env.actions)}
        for row in trace:
            assert row['before']==current['state']
            index=keys[row['action']['Key']]
            assert env.actions[index]==row['action'] and index in current['legal']
            following=env.step(index)
            assert following['state']==row['after'] and following['reward']==row['reward']
            current=following
        fixed=audit(trace);assert not fixed['errors'],fixed['errors']
        for label, arm, blue_source in [('no_arm',False,False),('token_fee_cap',True,True)]:
            current=env.reset_task(definition);actual=[]
            for step,row in enumerate(trace[:6]):
                if step==4 and not arm:continue
                index=keys[row['action']['Key']]
                if step==3 and blue_source:
                    index=next(i for i in current['legal'] if env.actions[i]['Kind']=='source' and env.actions[i]['Option']==1)
                assert index in current['legal']
                following=env.step(index);actual.append(dict(before=current['state'],action=env.actions[index],after=following['state'],reward=following['reward']));current=following
            report=audit(actual);assert not report['errors'],report['errors']
            if label=='no_arm':
                assert actual[-1]['before']['crystals']['Blue']==3 and current['state']['crystals']['Blue']==2
                assert 'hero_skill_pool_003:09' not in current['state']['usedSkillsRound']
            else:
                assert actual[-1]['before']['tokens']['Blue']==1 and current['state']['tokens']['Blue']==1
                assert current['state']['crystals']['Blue']==3 and current['state']['tokens']['White']==2
            path=out/(label+'.json');path.write_text(json.dumps(dict(definition=definition,trace=actual),ensure_ascii=False,indent=2),encoding='utf-8')
            cases.append(label)
        negatives=[]
        for label in ('missing_bonus','extra_overflow','wrong_public_color','wrong_owner','wrong_effect_output','waived_fee_activation'):
            bad=copy.deepcopy(trace[:6]);after=bad[5]['after'];before=bad[5]['before']
            if label=='missing_bonus':after['crystals']['Blue']=2
            elif label=='extra_overflow':after['tokens']['Blue']=1
            elif label=='wrong_public_color':after['sharedSkills']['tokens'][0]['color']='Red'
            elif label=='wrong_owner':after['sharedSkills']['tokens'][0]['owner']='Tovak'
            elif label=='wrong_effect_output':after['tokens']['White']=1
            else:before['artifactFlags']['infinite'].append('Blue')
            errors=audit(bad)['errors']
            assert any(e['step']==5 and ('conversion' in e['rule']) for e in errors),(label,errors)
            negatives.append(label)
        result.write_text(json.dumps(dict(status='actual_shared_conversion_original_oracle_fixed_not_rules_changed',
            seed=saved['seed'],task_id=saved['task_id'],original_failure_sha256=file_hash(failure),actual_replayed_operations=len(trace),
            old_errors=original['errors'],new_checks=fixed['checks'],new_errors=fixed['errors'],formal_counterparts=cases,rejected_negative_variants=negatives,
            scope='Original Blue fee1 + shared Blue crystal1 after payment, cap overflow exact; White2 and Gold effect die unchanged. Failure preserved. No new PPO update, full acceptance or Unity claim.'),indent=2),encoding='utf-8')
        print(json.loads(result.read_text()))
    finally:env.close()


if __name__=='__main__':main()
