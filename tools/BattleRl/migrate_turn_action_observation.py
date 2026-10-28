"""Explicit known observation migrations; never reuse unknown coordinates."""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np

OLD_SCHEMA = 'original_combat_six_targets_summon_parents_v37'
NEW_SCHEMA = 'original_combat_six_targets_summon_parents_turn_action_v46'
REPEATED_SCHEMA = 'original_combat_six_targets_summon_parents_repeated_v48'
NATIVE_PAYMENT_SCHEMA = REPEATED_SCHEMA + '_native_payment_selection_turn_obligation_v55'
COMBAT_CONVERSION_SCHEMA = NATIVE_PAYMENT_SCHEMA + '_combat_conversions_v58'
ENERGY_TARGET_SCHEMA = COMBAT_CONVERSION_SCHEMA + '_energy_unit_targets_v59'
EXPLOSIVE_SCHEMA = ENERGY_TARGET_SCHEMA + '_explosive_followup_v80'
IMPROVISATION_ARTIFACT_SCHEMA = EXPLOSIVE_SCHEMA + '_improvisation_artifact_materials_v92'
EFFECTIVE_FORTIFICATION_SCHEMA = IMPROVISATION_ARTIFACT_SCHEMA + '_effective_fortification_v94'


def migrate_effective_fortification(weights, old_info, new_info):
    if old_info.get('featureSchema') != IMPROVISATION_ARTIFACT_SCHEMA or new_info.get('featureSchema') != EFFECTIVE_FORTIFICATION_SCHEMA:
        raise ValueError('Expected explicit six public effective-fortification observation coordinates')
    if old_info['catalog'] != new_info['catalog']:
        raise ValueError('Fortification observation migration must retain every complete action definition and order')
    return append_factored_observation(weights, old_info, new_info, 5704, 5710)


def migrate_improvisation_artifacts(weights, old_info, new_info):
    if old_info.get('featureSchema') != EXPLOSIVE_SCHEMA or new_info.get('featureSchema') != IMPROVISATION_ARTIFACT_SCHEMA:
        raise ValueError('Expected the explicit original Improvisation artifact-material migration')
    old_keys = {action['Key'] for action in old_info['catalog']}
    added = {action['Key']: action for action in new_info['catalog'] if action['Key'] not in old_keys}
    original_items = json.loads((Path(__file__).resolve().parents[2] / 'resources/text_json/items.json').read_text(encoding='utf-8-sig'))
    item_ids = {item['id'] for item in original_items}
    if item_ids != {f'items_{index:03d}' for index in range(25)}:
        raise ValueError('Original artifact inventory changed; do not guess new material actions')
    expected = {}
    for action in old_info['catalog']:
        direct = action['Kind'] in ('play', 'repeat') and action['Card'] == 'basic_card_023'
        borrowed = action['Kind'] == 'play' and action['Card'] == 'advanced_card_024' and action['Input'].startswith('copy=basic_card_023;')
        if not (direct or borrowed) or 'material=basic_card_000' not in action['Input']:
            continue
        for item in sorted(item_ids):
            replacement = dict(action)
            replacement['Input'] = action['Input'].replace('material=basic_card_000', 'material=' + item)
            replacement['Key'] = action['Key'].replace('material=basic_card_000', 'material=' + item)
            expected[replacement['Key']] = replacement
    if not expected or added != expected:
        raise ValueError('Expected exactly all original artifact materials for Improvisation, its Overload host and repeat consumers')
    return append_factored_observation(weights, old_info, new_info, 5704, 5704)


def migrate_explosive_followup(weights, old_info, new_info):
    old_keys = {a['Key'] for a in old_info['catalog']}
    added = {a['Key']: a for a in new_info['catalog'] if a['Key'] not in old_keys}
    expected = {}
    for target in range(-1, 6):
        key = f'explosiveArmor:::{target}:0:0'
        expected[key] = dict(Kind='explosiveArmor', Card='', Choice='', Targets=target,
                             Parts=0, Option=0, Input='', Key=key)
    if added != expected:
        raise ValueError('Expected exactly six real enemy targets and explicit decline')
    return append_factored_observation(weights, old_info, new_info, 5703, 5704)


def migrate_energy_targets(weights, old_info, new_info):
    old_keys = {a['Key'] for a in old_info['catalog']}
    added = {a['Key']: a for a in new_info['catalog'] if a['Key'] not in old_keys}
    expected = {}
    for choice in ('base', 'strong'):
        for unit in range(3):
            key = f'play:magic_000:{choice}:0:0:0:unit={unit}'
            expected[key] = dict(Kind='play', Card='magic_000', Choice=choice,
                                 Targets=0, Parts=0, Option=0, Input=f'unit={unit}', Key=key)
    if added != expected:
        raise ValueError('Expected exactly six actual owned energy-spell unit targets')
    return append_factored_observation(weights, old_info, new_info, 5703, 5703)


def migrate_repeated(weights, old_info, new_info):
    return append_factored_observation(weights, old_info, new_info, 5591, 5670)


def migrate_native_payment(weights, old_info, new_info):
    old_keys = {a['Key'] for a in old_info['catalog']}
    added = [a for a in new_info['catalog'] if a['Key'] not in old_keys]
    if len(added) != 157 or any(a['Kind'] not in ('payment', 'cancelPayment') for a in added):
        raise ValueError('Expected only the documented shared native payment input actions')
    return append_factored_observation(weights, old_info, new_info, 5670, 5696)


def migrate_combat_conversion(weights, old_info, new_info):
    old_keys = {a['Key'] for a in old_info['catalog']}
    added = {a['Key']: a for a in new_info['catalog'] if a['Key'] not in old_keys}
    expected = {}
    for amount in range(9):
        for kind, card, choice in (('convertMovement', 'advanced_card_006', 'melee'),
                                   ('convertMovement', 'advanced_card_006', 'ranged'),
                                   ('convertInfluence', 'advanced_card_018', '')):
            key = f'{kind}:{card}:{choice}:0:0:{amount}'
            expected[key] = dict(Kind=kind, Card=card, Choice=choice, Targets=0,
                                 Parts=0, Option=amount, Input='', Key=key)
    if added != expected:
        raise ValueError('Expected exactly the 27 documented original combat conversion actions')
    return append_factored_observation(weights, old_info, new_info, 5696, 5703)


def append_factored_observation(weights, old_info, new_info, old_size, new_size):
    old_names = old_info.get('featureNames', [])
    new_names = new_info.get('featureNames', [])
    if (len(old_names) != old_size or len(new_names) != new_size or new_names[:old_size] != old_names):
        raise ValueError('Expected verified old coordinates followed by public pending-effect fields')
    if old_info.get('abilitySchema') != new_info.get('abilitySchema'):
        raise ValueError('Original ability layout must be identical')
    old_actions = old_info['catalog']
    new_actions = new_info['catalog']
    new_indices = {a['Key']: i for i, a in enumerate(new_actions)}
    if len(new_indices) != len(new_actions) or len({a['Key'] for a in old_actions}) != len(old_actions):
        raise ValueError('Action keys must be unique')
    for action in old_actions:
        if action['Key'] not in new_indices or new_actions[new_indices[action['Key']]] != action:
            raise ValueError('All original action definitions must remain identical by full Key')
    if (weights['w1'].shape[0] != old_size or weights['wp'].shape[1] != len(old_actions)
            or 'wk' not in weights or 'action_kind_groups' not in weights):
        raise ValueError('Expected the actual saved factored policy with verified original coordinates')
    old_kinds = sorted({a['Kind'] for a in old_actions})
    new_kinds = sorted({a['Kind'] for a in new_actions})
    expected_groups = np.array([old_kinds.index(a['Kind']) for a in old_actions])
    if not np.array_equal(weights['action_kind_groups'], expected_groups):
        raise ValueError('Saved kind labels do not match actual original action definitions')
    result = {k: v.copy() for k, v in weights.items()}
    result['w1'] = np.concatenate((weights['w1'], np.zeros((new_size-old_size, weights['w1'].shape[1]))))
    result['wp'] = np.zeros((weights['wp'].shape[0], len(new_actions)))
    result['bp'] = np.zeros(len(new_actions))
    for old_index, action in enumerate(old_actions):
        new_index = new_indices[action['Key']]
        result['wp'][:, new_index] = weights['wp'][:, old_index]
        result['bp'][new_index] = weights['bp'][old_index]
    result['wk'] = np.zeros((weights['wk'].shape[0], len(new_kinds)))
    result['bk'] = np.zeros(len(new_kinds))
    for old_index, kind in enumerate(old_kinds):
        new_index = new_kinds.index(kind)
        result['wk'][:, new_index] = weights['wk'][:, old_index]
        result['bk'][new_index] = weights['bk'][old_index]
    result['action_kind_groups'] = np.array([new_kinds.index(a['Kind']) for a in new_actions])
    return result


def migrate(weights, old_info, new_info):
    if old_info.get('featureSchema') == IMPROVISATION_ARTIFACT_SCHEMA and new_info.get('featureSchema') == EFFECTIVE_FORTIFICATION_SCHEMA:
        return migrate_effective_fortification(weights, old_info, new_info)
    if old_info.get('featureSchema') == EXPLOSIVE_SCHEMA and new_info.get('featureSchema') == IMPROVISATION_ARTIFACT_SCHEMA:
        return migrate_improvisation_artifacts(weights, old_info, new_info)
    if old_info.get('featureSchema') == ENERGY_TARGET_SCHEMA and new_info.get('featureSchema') == EXPLOSIVE_SCHEMA:
        return migrate_explosive_followup(weights, old_info, new_info)
    if old_info.get('featureSchema') == COMBAT_CONVERSION_SCHEMA and new_info.get('featureSchema') == ENERGY_TARGET_SCHEMA:
        return migrate_energy_targets(weights, old_info, new_info)
    if old_info.get('featureSchema') == NATIVE_PAYMENT_SCHEMA and new_info.get('featureSchema') == COMBAT_CONVERSION_SCHEMA:
        return migrate_combat_conversion(weights, old_info, new_info)
    if old_info.get('featureSchema') == REPEATED_SCHEMA and new_info.get('featureSchema') == NATIVE_PAYMENT_SCHEMA:
        return migrate_native_payment(weights, old_info, new_info)
    if old_info.get('featureSchema') == NEW_SCHEMA and new_info.get('featureSchema') == REPEATED_SCHEMA:
        return migrate_repeated(weights, old_info, new_info)
    if old_info.get('featureSchema') != OLD_SCHEMA or new_info.get('featureSchema') != NEW_SCHEMA:
        raise ValueError('Only the documented turn-action observation migration is supported')
    if old_info.get('abilitySchema') != new_info.get('abilitySchema'):
        raise ValueError('Original ability layout must be identical')
    if old_info.get('catalog') != new_info.get('catalog'):
        raise ValueError('Actual action indices and all input tuples must be identical')
    old_names = old_info.get('featureNames')
    new_names = new_info.get('featureNames')
    if (not old_names or len(old_names) != 5590 or new_names != old_names + ['state.5590']):
        raise ValueError('Expected exactly the verified 5590 coordinates plus the turn-action flag')
    if weights['w1'].shape[0] != len(old_names):
        raise ValueError('Weights do not match the old observation layout')
    if weights['wp'].shape[1] != len(old_info['catalog']):
        raise ValueError('Weights do not match the original action layout')
    migrated = {key: value.copy() for key, value in weights.items()}
    # Zero initialization preserves the saved policy and critic for either value
    # of the new public flag; subsequent PPO updates can learn its significance.
    migrated['w1'] = np.concatenate((weights['w1'], np.zeros((1, weights['w1'].shape[1]))))
    return migrated


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--model', required=True)
    parser.add_argument('--new-environment', required=True)
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    model = Path(args.model)
    new_environment = Path(args.new_environment)
    out = Path(args.out)
    old_info = json.loads((model.parent / 'environment.json').read_text(encoding='utf-8'))
    new_info = json.loads(new_environment.read_text(encoding='utf-8'))
    with np.load(model) as saved:
        weights = migrate(dict(saved), old_info, new_info)
    if (out / 'policy_ppo.npz').exists():
        raise ValueError('Preserve existing migration; choose a fresh destination')
    out.mkdir(parents=True, exist_ok=True)
    np.savez(out / 'policy_ppo.npz', **weights)
    (out / 'environment.json').write_text(json.dumps(new_info, indent=2), encoding='utf-8')
    provenance = {
        'source_model': str(model),
        'source_sha256': hashlib.sha256(model.read_bytes()).hexdigest(),
        'target_sha256': hashlib.sha256((out / 'policy_ppo.npz').read_bytes()).hexdigest(),
        'old_schema': old_info['featureSchema'], 'new_schema': new_info['featureSchema'],
        'coordinate_mapping': 'all original input coordinates unchanged; append zero-weight public state fields',
        'action_mapping': 'identical full original action Keys; kind-head columns mapped by original labels',
        'new_input_weights': 'zero rows; added action and kind logits start at zero',
        'policy_limit': 'common-mask original decisions preserved; genuinely new legal actions can change probabilities',
        'optimizer_state_restored': False,
        'scope': 'weight compatibility only, not improved policy, rule coverage or Unity acceptance',
    }
    (out / 'migration.json').write_text(json.dumps(provenance, indent=2), encoding='utf-8')
    print(json.dumps(provenance))


if __name__ == '__main__':
    main()
