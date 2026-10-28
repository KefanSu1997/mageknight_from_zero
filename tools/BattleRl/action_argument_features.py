"""Public catalog argument descriptors, never action previews or future state.

Deterministic signed label features share parameters across card/skill IDs,
choices and input fields; numeric slots/masks remain explicit. These are not
parsed rule effects and must not be described as original rule understanding.
"""
from functools import lru_cache
import hashlib
import numpy as np


@lru_cache(maxsize=32768)
def label_vector(label):
    result = np.zeros(16)
    label = str(label)
    if not label:
        return result
    pieces = [label] + [label[i:i + 2] for i in range(max(0, len(label) - 1))]
    for piece in pieces:
        digest = hashlib.sha256(piece.encode('utf-8')).digest()
        result[digest[0] % 16] += 1 if digest[1] & 1 else -1
    result /= max(1., np.linalg.norm(result))
    result.setflags(write=False)
    return result


def catalog_argument_features(actions):
    result = np.zeros((len(actions), 96), dtype=np.float32)
    prefixes = ('basic_card_', 'advanced_card_', 'magic_', 'items_', 'hero_skill_pool_', 'army_', 'monster_')
    for index, action in enumerate(actions):
        for field, start in (('Kind', 0), ('Card', 16), ('Choice', 32), ('Input', 48)):
            result[index, start:start + 16] = label_vector(action.get(field, ''))
        targets, parts, option = (int(action.get(key, 0)) for key in ('Targets', 'Parts', 'Option'))
        result[index, 64:67] = np.clip([targets, parts, option], -32, 32) / 16.
        result[index, 67:73] = [(targets >= 0 and bool(targets & (1 << bit))) for bit in range(6)]
        result[index, 73:81] = [(parts >= 0 and bool(parts & (1 << bit))) for bit in range(8)]
        result[index, 81:83] = [int(result[index, 67:73].sum()) / 6., int(result[index, 73:81].sum()) / 8.]
        result[index, 83:90] = [str(action.get('Card', '')).startswith(prefix) for prefix in prefixes]
    return result
