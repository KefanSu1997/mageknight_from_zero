"""Opt-in equivalent numeric descriptor cache; not installed in live runs.

Only catalog descriptors are cached. Public observations, legal commands,
printed-rule bindings and scenario-specific coordinates remain uncached.
The caller receives its own writable array, never the cached array.
"""
from functools import lru_cache
import numpy as np
from action_argument_features import catalog_argument_features as uncached_features

FIELDS = ('Kind', 'Card', 'Choice', 'Input', 'Targets', 'Parts', 'Option')


@lru_cache(maxsize=16384)
def _row(key):
    result = uncached_features([dict(zip(FIELDS, key))])[0]
    result.setflags(write=False)
    return result


def catalog_argument_features(actions):
    if not actions:
        return np.zeros((0, 96), dtype=np.float32)
    keys = [tuple(action.get(k, 0 if k in FIELDS[4:] else '')
                  for k in FIELDS) for action in actions]
    return np.stack([_row(key) for key in keys])


def cache_info():
    return _row.cache_info()
