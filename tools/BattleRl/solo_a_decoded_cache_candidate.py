"""Opt-in bounded immutable decoded contexts; no policy or rule changes."""
import base64
import copy
import hashlib
import json
import sys
import zlib
from collections import OrderedDict

import numpy as np

from solo_a_binary_context_candidate import MAGIC
from solo_a_binary_runtime_candidate import BinaryScenarioRuntime


def retained_bytes(value):
    seen = set()
    def size(x):
        if id(x) in seen:
            return 0
        seen.add(id(x))
        if isinstance(x, np.ndarray):
            return sys.getsizeof(x) + (x.nbytes if x.base is not None else 0)
        if isinstance(x, dict):
            return sys.getsizeof(x) + sum(size(k) + size(v) for k, v in x.items())
        if isinstance(x, (list, tuple)):
            return sys.getsizeof(x) + sum(map(size, x))
        return sys.getsizeof(x)
    return size(value)


class DecodedCacheRuntime(BinaryScenarioRuntime):
    def __init__(self, path, binding, cache_bytes=256*2**20):
        self.decoded = OrderedDict()
        self.decoded_bytes = 0
        self.decoded_limit = cache_bytes
        self.decoded_hits = 0
        self.decoded_misses = 0
        super().__init__(path, binding)

    def _remember(self, context, value):
        size = retained_bytes(value)
        if context in self.decoded:
            self.decoded_bytes -= self.decoded.pop(context)[1]
        if size <= self.decoded_limit:
            self.decoded[context] = (value, size)
            self.decoded_bytes += size
        while self.decoded_bytes > self.decoded_limit:
            _, (_, size) = self.decoded.popitem(last=False)
            self.decoded_bytes -= size
        return value

    def add(self, support, document):
        context = super().add(support, document)
        # Isolate against later caller mutation, but do not expand float arrays
        # into Python scalar/list graphs just to immediately decode them again.
        value = copy.deepcopy(dict(support=support, document=document))
        for key in ('descriptors', 'history_features'):
            value['support'][key].setflags(write=False)
        self._remember(context, value)
        return context

    def _read(self, context):
        context = int(context)
        if context in self.decoded:
            self.decoded_hits += 1
            self.decoded.move_to_end(context)
            return self.decoded[context][0]
        self.decoded_misses += 1
        row = self.db.execute('SELECT sha,payload FROM contexts WHERE id=?', (context,)).fetchone()
        if row is None:
            raise ValueError('Missing immutable context')
        payload = bytes(row[1])
        if not payload.startswith(MAGIC):
            return self._remember(context, super()._read(context))
        raw = zlib.decompress(payload[len(MAGIC):])
        if hashlib.sha256(raw).hexdigest() != row[0]:
            raise ValueError('Context SHA mismatch')
        data = json.loads(raw)
        if data['version'] != 1 or set(data['arrays']) != {'descriptors', 'history_features'}:
            raise ValueError('Unsupported candidate context arrays')
        for key, array in data['arrays'].items():
            if array['dtype'] != '<f4':
                raise ValueError('Unexpected candidate array dtype')
            values = np.frombuffer(base64.b64decode(array['data'], validate=True), dtype='<f4')
            data['support'][key] = values.reshape(array['shape'])
            if not np.isfinite(data['support'][key]).all():
                raise ValueError('Nonfinite candidate array')
        data['support']['commands'] = tuple(data['support']['commands'])
        return self._remember(context, dict(support=data['support'], document=data['document']))
