"""Public original printed descriptions, never a rule simulator or future preview.

Signed character/character-pair features provide shared public text to a later
actor experiment. Hashes themselves do not prove understanding or learning.
"""
from collections import Counter
import hashlib
import json
from pathlib import Path
import numpy as np


def text_vector(text, width=64):
    if not isinstance(text, str) or width <= 0:
        raise ValueError('Explicit original text and positive width required')
    compact = ''.join(text.split())
    result = np.zeros(width, dtype=np.float32)
    pieces = Counter(compact)
    pieces.update(compact[i:i + 2] for i in range(len(compact) - 1))
    for piece, count in pieces.items():
        hashed = hashlib.sha256(piece.encode('utf-8')).digest()
        result[int.from_bytes(hashed[:4], 'little') % width] += count * (1 if hashed[4] & 1 else -1)
    norm = np.linalg.norm(result)
    if norm:
        result /= norm
    return result


def public_description(value):
    if isinstance(value, dict):
        return '|'.join(str(key) + ':' + public_description(item) for key, item in value.items()
                        if key not in ('id', 'set', 'set_idx', 'image', 'en_image', 'wiki'))
    if isinstance(value, list):
        return '|'.join(public_description(item) for item in value)
    return str(value)


class PrintedEntityText:
    def __init__(self, ids, root=None):
        root = Path(root) if root else Path(__file__).resolve().parents[2]
        folder = root / 'resources/text_json'
        self.ids = list(ids)
        if len(set(self.ids)) != len(self.ids):
            raise ValueError('Duplicate public entity IDs')
        descriptions, signatures = {}, {}
        for name in ('basic_card', 'advanced_card', 'magic', 'items', 'skill', 'army', 'army_2', 'monster'):
            path = folder / (name + '.json')
            raw = path.read_bytes()
            signatures[name] = hashlib.sha256(raw).hexdigest()
            rows = json.loads(raw)
            for row in rows:
                identity = row['id']
                if name in ('basic_card', 'advanced_card'):
                    pair = (row.get('base_effect', ''), row.get('required_crystals', '') + '|' + row.get('enhanced_effect', ''))
                elif name == 'magic':
                    pair = tuple(row[section].get('mana_cost', '') + '|' + row[section]['effect']
                                 for section in ('spell_top', 'spell_bottom'))
                elif name == 'items':
                    # The source field name is historical: its second printed
                    # paragraph is burn, not a guessed once-per-round rule.
                    pair = (row['assign_effect'], row['once_per_round'])
                elif name == 'skill':
                    for index, skill in enumerate(row['skills']):
                        source = f'{identity}:{index:02d}'
                        if source not in self.ids:
                            continue
                        if 'total_description' in skill:
                            pair = (skill['total_description'], '')
                        else:
                            pair = (public_description(skill['side1_skill']), public_description(skill['side2_skill']))
                        descriptions[source] = pair
                    continue
                else:
                    pair = (public_description(row), '')
                if identity in descriptions:
                    raise ValueError('Ambiguous original public descriptor: ' + identity)
                descriptions[identity] = pair
        self.table = np.zeros((len(self.ids), 128), dtype=np.float32)
        for index, identity in enumerate(self.ids):
            if identity == '_public_global_':
                continue
            if identity not in descriptions:
                raise ValueError('Original printed source is missing: ' + identity)
            first, second = descriptions[identity]
            self.table[index] = np.concatenate((text_vector(first), text_vector(second)))
        self.table.setflags(write=False)
        self.signature = dict(source_sha256=signatures, public_ids=self.ids,
            feature_schema='original_printed_char_bigram_64_first_face_64_second_face_v1',
            scope='Static public original descriptions only; no hidden state, seed, preview or teacher labels')

    def pack(self, public_tokens, present):
        public_tokens, present = np.asarray(public_tokens), np.asarray(present, dtype=bool)
        if public_tokens.ndim != 3 or public_tokens.shape[:2] != present.shape or public_tokens.shape[-1] < len(self.ids):
            raise ValueError('Expected public entity tokens and their actual presence mask')
        identity = np.where(present[..., None], public_tokens[..., :len(self.ids)], 0.)
        if not np.isfinite(identity).all() or not np.all((identity == 0) | (identity == 1)):
            raise ValueError('Public identity must be finite and discrete')
        if not np.all(identity.sum(axis=-1)[present] == 1):
            raise ValueError('An actual entity must have exactly one known identity')
        return identity @ self.table
