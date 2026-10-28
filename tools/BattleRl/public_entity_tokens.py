"""Staged Transformer tokens from the existing public feature vector only.

Coordinates describe the frozen 6314-feature world protocol, not arbitrary
chunks of a flat vector. Original flat features must also remain in the actor:
these tokens supplement them and do not replace pending choices or pointers.
No state dictionary, physical audit IDs, hidden draw order or seed is accepted.
"""
import json
import re
from pathlib import Path
import numpy as np

WORLD_SCHEMA = ('original_combat_six_targets_summon_parents_repeated_v48'
    '_native_payment_selection_turn_obligation_v55_combat_conversions_v58'
    '_energy_unit_targets_v59_explosive_followup_v80_improvisation_artifact_materials_v92'
    '_effective_fortification_v94_independent_task_progress_v1_shared_seven_owner_inputs_v2'
    '_native_shapeshift_v1_original_tutorial_world_v1')
KINDS = ('global', 'card', 'skill', 'unit', 'enemy')


def original_vocabulary(root=None):
    root = Path(root) if root else Path(__file__).resolve().parents[2]
    folder = root / 'resources/text_json'
    def ids(name):
        return [row['id'] for row in json.loads((folder / name).read_text(encoding='utf-8-sig'))]
    inventory = json.loads((root / 'AutomationConfigs/rl_full_coverage_inventory.json').read_text())
    hero_source = (root / 'Assets/Logic/Data/OriginalHeroes.cs').read_text(encoding='utf-8-sig')
    hero_order = re.findall(r'new OriginalHero\("([^"]+)"', hero_source)
    if hero_order != [hero['Id'] for hero in inventory['heroes']]:
        raise ValueError('Original skill-owner order differs from the frozen feature producer')
    cards = inventory['cards']
    advanced_source = ids('advanced_card.json')
    unit_source = (root / 'Assets/Logic/Data/UnitJsonLoader.cs').read_text(encoding='utf-8-sig')
    unit_ids = re.findall(r'"(army[^" ]+)"', re.search(r'SupportedIds\s*=\s*\{(.*?)\}', unit_source, re.S).group(1))
    monsters = json.loads((folder / 'monster.json').read_text(encoding='utf-8-sig'))
    result = dict(basic=ids('basic_card.json'),
                  advanced=[row['id'] for row in cards if row['category'] == 'advanced_card'],
                  spell=ids('magic.json'), item=ids('items.json'),
                  unit=unit_ids,
                  enemy=[row['id'] for row in monsters if row.get('attack') and isinstance(row.get('armor'), dict)],
                  skill=[s['SourceId'] for h in inventory['heroes'] for s in h['skills']])
    if [len(result[key]) for key in ('basic', 'advanced', 'spell', 'item', 'unit', 'enemy', 'skill')] != [28, 44, 24, 25, 30, 69, 70]:
        raise ValueError('Original entity vocabulary changed; a new verified layout is required')
    if len(advanced_source) != 48 or advanced_source[:44] != result['advanced']:
        raise ValueError('Frozen raw advanced coordinates differ from original44; do not guess offsets')
    return result


class PublicEntityTokens:
    def __init__(self, vocabulary, feature_schema=WORLD_SCHEMA, observation_size=6314):
        if feature_schema != WORLD_SCHEMA or observation_size != 6314:
            raise ValueError('Entity coordinates only support the exact verified world schema')
        self.vocabulary = vocabulary
        self.ids = ['_public_global_'] + [identity for key in
            ('basic', 'advanced', 'spell', 'item', 'skill', 'unit', 'enemy') for identity in vocabulary[key]]
        if len(self.ids) != len(set(self.ids)) or len(self.ids) != 291:
            raise ValueError('Original entity identities must be unique and complete')
        self.id_index = {identity: i for i, identity in enumerate(self.ids)}
        self.width = len(self.ids) + len(KINDS) + 6 + 48
        self.numeric_start = len(self.ids) + len(KINDS) + 6
        self.observation_size = observation_size
        # Derived from JourneyTraining.Features: 6 colors, 22 abilities,
        # 3 attack types, six enemy slots. Runtime projections are checked
        # independently before this layout may enter an actual PPO actor.
        self.card_counts = ([(identity, 37 + i, 2.) for i, identity in enumerate(vocabulary['basic'])]
            + [(identity, 158 + i, 2.) for i, identity in enumerate(vocabulary['advanced'])]
            + [(identity, 1997 + i, 2.) for i, identity in enumerate(vocabulary['spell'])]
            + [(identity, 3190 + 5 * i, 1.) for i, identity in enumerate(vocabulary['item'])])

    def _token(self, identity, kind, numbers, slot=None):
        token = np.zeros(self.width)
        token[self.id_index[identity]] = 1.
        token[len(self.ids) + KINDS.index(kind)] = 1.
        if slot is not None:
            token[len(self.ids) + len(KINDS) + slot] = 1.
        token[self.numeric_start:self.numeric_start + len(numbers)] = numbers
        return token

    def _one(self, feature):
        if np.any(feature[202:206] != 0):
            raise ValueError('Raw advanced044-047 have no original inventory text; retain source gap')
        rows = [self._token('_public_global_', 'global', feature[:18])]
        identities = [('_public_global_', 'global', None)]
        def append(identity, kind, numbers, slot=None):
            rows.append(self._token(identity, kind, numbers, slot))
            identities.append((identity, kind, slot))
        for identity, coordinate, scale in self.card_counts:
            count = feature[coordinate] * scale
            if count > 0:
                append(identity, 'card', [count])
        for i, identity in enumerate(self.vocabulary['skill']):
            fields = feature[207 + 4 * i:211 + 4 * i]
            if fields[0] > 0:
                append(identity, 'skill', fields)
        owned_count = sum(feature[2102 + 16 * i] > 0 for i in range(len(self.vocabulary['unit'])))
        public_slots_complete = all(np.count_nonzero(feature[2847 + slot * 30:2877 + slot * 30]) == 1 for slot in range(3))
        for i, identity in enumerate(self.vocabulary['unit']):
            fields = feature[2102 + 16 * i:2118 + 16 * i]
            if fields[0] > 0:
                # Existing public slot-ID observations; no inferred physical ID.
                slots = [slot for slot in range(3) if feature[2847 + slot * 30 + i] > 0]
                if not slots and owned_count > 3 and public_slots_complete:
                    # A legal acquisition can add a fourth owned unit. Its
                    # public per-ID fields exist, but this protocol only emits
                    # three slot pointers. Keep the unit without inventing a
                    # physical slot or silently dropping its public state.
                    append(identity, 'unit', fields, None)
                    continue
                if len(slots) != 1:
                    raise ValueError('Current protocol needs exactly one public slot for an owned unit')
                append(identity, 'unit', fields, slots[0])
        for slot in range(6):
            start = 487 + slot * 110
            if feature[start] <= 0:
                continue
            onehot = feature[start + 2:start + 71]
            active = np.flatnonzero(onehot > 0)
            if len(active) != 1:
                raise ValueError('Present enemy must identify exactly one original ID')
            numbers = np.concatenate([feature[start:start + 2], feature[start + 71:start + 110],
                                      [feature[1779 + slot * 35], feature[5704 + slot]]])
            append(self.vocabulary['enemy'][active[0]], 'enemy', numbers, slot)
        return np.stack(rows), identities

    def pack(self, features):
        features = np.asarray(features, dtype=np.float64)
        if features.ndim != 2 or features.shape[-1] != self.observation_size or not np.isfinite(features).all():
            raise ValueError('Only finite [batch,6314] public observations are accepted')
        packed = [self._one(row) for row in features]
        maximum = max(len(rows) for rows, _ in packed)
        tokens = np.zeros((len(packed), maximum, self.width))
        present = np.zeros((len(packed), maximum), dtype=bool)
        for batch, (rows, _) in enumerate(packed):
            tokens[batch, :len(rows)] = rows
            present[batch, :len(rows)] = True
        return tokens, present, [identities for _, identities in packed]

    def scatter_numeric_gradient(self, token_gradient, identities):
        """Differentiate numeric fields; presence/ID/slot selection is discrete.

        Tokens have fixed original identities selected from public one-hot
        coordinates. They are observations, not soft trainable ID assignments.
        """
        dx = np.zeros((len(identities), self.observation_size))
        cards = {identity: (coordinate, scale) for identity, coordinate, scale in self.card_counts}
        for batch, entries in enumerate(identities):
            for index, (identity, kind, slot) in enumerate(entries):
                derivative = token_gradient[batch, index, self.numeric_start:]
                if kind == 'global':
                    dx[batch, :18] += derivative[:18]
                elif kind == 'card':
                    coordinate, scale = cards[identity]
                    dx[batch, coordinate] += derivative[0] * scale
                elif kind in ('skill', 'unit'):
                    i = self.vocabulary[kind].index(identity)
                    start, size = (207 + 4 * i, 4) if kind == 'skill' else (2102 + 16 * i, 16)
                    dx[batch, start:start + size] += derivative[:size]
                elif kind == 'enemy':
                    start = 487 + slot * 110
                    coordinates = [start, start + 1, *range(start + 71, start + 110),
                                   1779 + slot * 35, 5704 + slot]
                    dx[batch, coordinates] += derivative[:len(coordinates)]
                else:
                    raise ValueError('Unknown public entity kind')
        return dx
