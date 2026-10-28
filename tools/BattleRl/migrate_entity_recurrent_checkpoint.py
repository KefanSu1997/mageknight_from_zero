"""Explicit actor expansion; no training, no old coverage relabeling."""
import argparse
import json
import os
from pathlib import Path
import numpy as np
from recurrent_checkpoint import load_checkpoint, save_checkpoint, Adam
from recurrent_policy import RecurrentPolicy
from entity_recurrent_policy import EntityRecurrentPolicy
from public_entity_tokens import PublicEntityTokens, original_vocabulary
from mixed_task_pool import validate_pool
from original_data_signature import data_signature, world_source_hash
from task_feasibility import digest, file_hash
from recurrent_architecture import build_policy


def main():
    parser = argparse.ArgumentParser()
    for name in ('source', 'pool', 'dll', 'out'):
        parser.add_argument('--' + name, required=True)
    args = parser.parse_args()
    out = Path(args.out)
    if out.exists():
        raise ValueError('Preserve migration evidence')
    with np.load(args.source, allow_pickle=False) as archive:
        metadata = json.loads(str(archive['header']))['metadata']
    if metadata['rules_sha256'] != file_hash(args.dll) or metadata['pool_sha256'] != file_hash(args.pool):
        raise ValueError('Actual source rule/pool binding differs')
    if metadata['rules_data_sha256'] != data_signature()['sha256'] or metadata['world_source_sha256'] != world_source_hash():
        raise ValueError('Actual original data binding differs')
    for key, source in (('recurrent_policy_sha256', 'recurrent_policy.py'), ('recurrent_core_sha256', 'recurrent_core.py')):
        if metadata[key] != file_hash(Path(__file__).with_name(source)):
            raise ValueError('Saved recurrent actor code differs')
    parameters, optimizer, random, replay = load_checkpoint(args.source, metadata)
    if not replay['at_native_episode_boundary']:
        raise ValueError('Explicit actual episode boundary required')
    pool = json.loads(Path(args.pool).read_text())
    rows = validate_pool(pool, args.dll)
    os.environ.update(BATTLE_RL_DLL=str(Path(args.dll).resolve()), BATTLE_RL_PROTOCOL='--serve-journey-artifacts',
        BATTLE_RL_NATIVE_PAYMENTS='1', BATTLE_RL_NATIVE_CONVERSIONS='1', BATTLE_RL_NATIVE_ENERGY_TARGETS='1',
        BATTLE_RL_ORIGINAL_OPENINGS='1', BATTLE_RL_SHARED_SKILLS='1', BATTLE_RL_SHAPESHIFT='1', BATTLE_RL_ORIGINAL_WORLD='1')
    from train_ppo import Rules
    rules = Rules()
    try:
        current = rules.reset_task(rows[0]['task']['definition'])
        if metadata['feature_schema'] != rules.info['featureSchema'] or metadata['catalog_sha256'] != digest(rules.actions):
            raise ValueError('Actual catalog/schema differs')
        size = len(current['features'])
        layout = PublicEntityTokens(original_vocabulary(), rules.info['featureSchema'], size)
        old = RecurrentPolicy(size, rules.actions)
        old.bind_parameters(parameters)
        new, architecture_metadata = build_policy(size, rules.actions, rules.info['featureSchema'], True)
        updated = new.parameters()
        first = {key: np.zeros_like(value) for key, value in updated.items()}
        second = {key: np.zeros_like(value) for key, value in updated.items()}
        for key, value in parameters.items():
            if key == 'core.encoder_w0':
                updated[key][...] = 0.
                updated[key][:len(value)] = value
                first[key][:len(value)] = optimizer.first[key]
                second[key][:len(value)] = optimizer.second[key]
            else:
                updated[key][...] = value
                first[key][...] = optimizer.first[key]
                second[key][...] = optimizer.second[key]
        other = Adam(updated, **optimizer.settings())
        other.first, other.second, other.steps = first, second, optimizer.steps
        previous, reward = -1, 0.
        old_memory = new_memory = None
        maximum = 0.
        operations = []
        for _ in range(6):
            a, value, old_memory = old.step(current['features'], current['legal'], previous, reward, old_memory)
            b, other_value, new_memory = new.step(current['features'], current['legal'], previous, reward, new_memory)
            difference = max(float(np.max(np.abs(a - b))), abs(value - other_value), float(np.max(np.abs(old_memory - new_memory))))
            maximum = max(maximum, difference)
            if difference > 1e-12:
                raise ValueError('Initial complete legal policy/value/GRU changed')
            action = next((i for i in current['legal'] if rules.actions[i]['Kind'] in ('fight', 'advance')), current['legal'][0])
            operations.append(rules.actions[action]['Key'])
            following = rules.step(action)
            previous, reward, current = action, following['reward'], following
            if current['done']:
                break
        next_metadata = dict(metadata, entity_transformer=True,
            architecture='EntityTransformer32_heads4_MLP256-256-128_GRU256_factored' + str(len({a['Kind'] for a in rules.actions})) + '_original_catalog',
            entity_width=32, entity_heads=4, entity_vocabulary_sha256=digest(layout.vocabulary))
        for key, source in (('entity_transformer_sha256', 'entity_transformer.py'),
                            ('entity_recurrent_policy_sha256', 'entity_recurrent_policy.py'),
                            ('public_entity_tokens_sha256', 'public_entity_tokens.py')):
            next_metadata[key] = file_hash(Path(__file__).with_name(source))
        next_metadata.update(architecture_metadata)
        out.mkdir(parents=True)
        save_checkpoint(out / 'checkpoint.npz', other, random, next_metadata, replay)
        arrays, restored, rng, history = load_checkpoint(out / 'checkpoint.npz', next_metadata)
        for key in updated:
            for a, b in ((updated[key], arrays[key]), (first[key], restored.first[key]), (second[key], restored.second[key])):
                np.testing.assert_array_equal(a, b)
        assert rng.bit_generator.state == random.bit_generator.state and history == replay and restored.steps == optimizer.steps
        report = dict(status='saved_actor_entity_expansion_no_learning', array_roundtrip_exact=3 * len(updated),
            optimizer_rng_actual_history_preserved=True, actual_full_legal_operations=operations,
            maximum_policy_value_memory_difference=maximum, source_checkpoint_sha256=file_hash(args.source),
            new_checkpoint_sha256=file_hash(out / 'checkpoint.npz'),
            scope='Zero new encoder columns retain existing policy; attention/Adam added explicitly. Not trained Transformer or generalization/Unity acceptance; trainer and frozen-reader integration pending.')
        (out / 'migration.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        print(json.dumps(report))
    finally:
        rules.close()


if __name__ == '__main__':
    main()
