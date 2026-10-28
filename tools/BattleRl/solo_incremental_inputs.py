"""Per-episode exact dependency caches for public entity projections.

This does not cache legal supports, hashes, history or terminal flags. Call
for_episode once per environment episode; never share its result across workers.
"""
import copy
import numpy as np
from baseline_a_visible_inventory import CARD_ZONES, OFFER_ZONES


def same(a, b):
    if type(a) is not type(b):
        return False
    if isinstance(a, np.ndarray):
        return a.dtype == b.dtype and a.shape == b.shape and a.tobytes() == b.tobytes()
    if isinstance(a, dict):
        return a.keys() == b.keys() and all(same(a[k], b[k]) for k in a)
    if isinstance(a, (list, tuple)):
        return len(a) == len(b) and all(same(x, y) for x, y in zip(a, b))
    return a == b


def freeze_arrays(value):
    if isinstance(value, np.ndarray):
        value.setflags(write=False)
    elif isinstance(value, dict):
        for x in value.values(): freeze_arrays(x)
    elif isinstance(value, (list, tuple)):
        for x in value: freeze_arrays(x)


class ProjectionCache:
    def __init__(self, target, method='encode', dependency=None):
        self.target, self.method, self.dependency = target, method, dependency
        self.ready = False
        self.hits = self.misses = 0

    def __getattr__(self, name):
        if name == self.method:
            return self.project
        return getattr(self.target, name)

    def project(self, value):
        key = value if self.dependency is None else self.dependency(value)
        if self.ready and same(key, self.snapshot):
            self.hits += 1
        else:
            # Compute first: a validation failure must not publish a cache entry.
            output = getattr(self.target, self.method)(value)
            self.snapshot = copy.deepcopy(key)
            self.output = copy.deepcopy(output)
            freeze_arrays(self.output)
            self.ready = True
            self.misses += 1
        # Caller only reads nested records; prevent its top-level concatenation
        # from growing the cache's list. Numeric outputs are explicitly readonly.
        return list(self.output) if isinstance(self.output, list) else self.output


def visible_dependency(observation):
    return ({k: observation['Inventory'][k] for k in (*CARD_ZONES, 'Skills')},
            {k: observation['Offers'][k] for k in OFFER_ZONES})


def for_episode(inputs):
    clone = copy.copy(inputs)
    clone.layout = ProjectionCache(inputs.layout, '_one')
    clone.world = ProjectionCache(inputs.world)
    clone.visible = ProjectionCache(inputs.visible, dependency=visible_dependency)
    clone.combat = ProjectionCache(inputs.combat)
    clone.resources = ProjectionCache(inputs.resources)
    clone.pending = ProjectionCache(inputs.pending)
    return clone
