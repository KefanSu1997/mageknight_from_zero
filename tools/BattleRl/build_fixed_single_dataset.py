"""Freeze 4,000 train / 1,000 evaluation compositions via real native resets."""
import argparse
import copy
import json
import os
from pathlib import Path
from collections import Counter
import numpy as np
from task_feasibility import digest, file_hash, SEQUENCE_GOAL
from coverage_tasks import combination, split
from original_data_signature import data_signature


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--dll', required=True)
    p.add_argument('--base-pool', required=True)
    p.add_argument('--out', required=True)
    p.add_argument('--resume-admissions', action='store_true')
    a = p.parse_args()
    out = Path(a.out)
    out.mkdir(exist_ok=a.resume_admissions)
    if (out/'manifest.json').exists():raise ValueError('Dataset is already frozen')
    os.environ.update(BATTLE_RL_DLL=str(Path(a.dll).resolve()), BATTLE_RL_PROTOCOL='--serve-journey-artifacts',
        BATTLE_RL_NATIVE_PAYMENTS='1', BATTLE_RL_NATIVE_CONVERSIONS='1', BATTLE_RL_NATIVE_ENERGY_TARGETS='1',
        BATTLE_RL_ORIGINAL_OPENINGS='1', BATTLE_RL_SHARED_SKILLS='1', BATTLE_RL_SHAPESHIFT='1', BATTLE_RL_ORIGINAL_WORLD='1')
    from train_ppo import Rules
    from dynamic_single_curriculum import DynamicSingleCurriculum
    from mixed_task_pool import validate_pool
    pool = json.loads(Path(a.base_pool).read_text())
    base_rows = validate_pool(pool, a.dll)
    unique = {}
    for row in base_rows:
        unique.setdefault(digest(combination(row['task']['definition'])), row)
    rows = list(unique.values())
    assert all(len(r['task']['definition']['EnemyGroups']) == 1 for r in rows)
    train = list(rows)
    evaluation = []
    seen = {digest(combination(r['task']['definition'])) for r in rows}
    if a.resume_admissions:
        for name, target in [('train_admissions.jsonl', train), ('validation_admissions.jsonl', evaluation)]:
            path = out/name
            if not path.exists():continue
            for line in path.read_text(encoding='utf-8').splitlines():
                row = json.loads(line)
                key = digest(combination(row['task']['definition']))
                if row['data_signature'] != data_signature() or row['task']['rules_sha256'] != file_hash(a.dll):
                    raise ValueError('Cannot reuse stale admission')
                if key not in seen:
                    target.append(row); seen.add(key)
    rules = Rules()
    data = data_signature()
    generator = DynamicSingleCurriculum(rows, rules, out)
    generator.random = np.random.default_rng(8112026)
    def append(name, row):
        with (out/name).open('a', encoding='utf-8') as stream:
            stream.write(json.dumps(row, ensure_ascii=False)+'\n')
    try:
        while len(train) < 4000 or len(evaluation) < 1000:
            definition = generator.draft()
            key = digest(combination(definition))
            partition = split(definition)
            if key in seen or partition == 'test':
                continue
            if partition == 'train' and len(train) >= 4000 or partition == 'validation' and len(evaluation) >= 1000:
                continue
            seen.add(key)
            try:
                root = rules.reset_task(definition)
            except RuntimeError as error:
                append('rejected.jsonl', dict(definition=definition, error=str(error), status='invalid_or_adapter_failure_not_unsolvable'))
                continue
            if root['done'] or not root['legal']:
                append('rejected.jsonl', dict(definition=definition, status='terminal_root_unknown'))
                continue
            binding = dict(generator='independent_original_factors_v1', seed=definition['RandomSeed'], definition=definition,
                rules_sha256=generator.rules_sha, feature_schema=rules.info['featureSchema'], catalog_sha256=generator.catalog_sha,
                initial_state_sha256=digest(root['state']), goal=SEQUENCE_GOAL)
            row = dict(task_id=digest(binding), composition_id=key, task=binding, hero=root['state']['hero'],
                status='legal_reset_unknown', data_signature=data,
                admitter_sha256=file_hash(Path(__file__).with_name('mixed_task_pool.py')),
                admission=dict(legal_actions=len(root['legal']), native_done=False),
                scope='Legal native reset only; unknown solvability is retained in the denominator')
            target = train if partition == 'train' else evaluation
            target.append(row)
            append(partition+'_admissions.jsonl', row)
            if (len(train)+len(evaluation)) % 50 == 0:
                (out/'progress.json').write_text(json.dumps(dict(train=len(train), evaluation=len(evaluation), drafts=generator.generated)))
        assert data_signature() == data
    finally:
        rules.close()
    train_keys = {digest(combination(r['task']['definition'])) for r in train}
    eval_keys = {digest(combination(r['task']['definition'])) for r in evaluation}
    assert len(train_keys) == 4000 and len(eval_keys) == 1000 and not train_keys & eval_keys
    training = copy.deepcopy(pool)
    training.update(tasks=[r for r in train if r['status']!='legal_reset_unknown'], unknown_tasks=[r for r in train if r['status']=='legal_reset_unknown'])
    validate_pool(training, a.dll)
    (out/'train.json').write_text(json.dumps(training, ensure_ascii=False), encoding='utf-8')
    (out/'eval.json').write_text(json.dumps(dict(partition='validation', goal=SEQUENCE_GOAL,
        tasks=evaluation, selection='frozen before training; all 1000 legal roots, including unknown solvability'), ensure_ascii=False), encoding='utf-8')
    report = dict(train=4000, evaluation=1000, total=5000, overlap=0, existing_train_anchors=len(rows),
        new_native_resets=5000-len(rows), generation_seed=8112026,
        train_heroes=dict(Counter(r['hero'] for r in train)), eval_heroes=dict(Counter(r['hero'] for r in evaluation)),
        train_sha256=file_hash(out/'train.json'), eval_sha256=file_hash(out/'eval.json'),
        evaluation_not_final_test=True, unknown_does_not_mean_impossible=True,
        scope='one battle, full native cleanup; difficulty curriculum only changes training sampling probabilities')
    (out/'manifest.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(report), flush=True)


if __name__ == '__main__':
    main()
