"""Exact component-row sharing for an isolated training view.

The persisted support remains unchanged. All candidates, component weights,
history features, operations and rule bindings are retained. Only descriptor
row indices are remapped; each candidate still reads identical float32 bytes.
"""
import numpy as np
import base64
import hashlib
import json
import zlib
from solo_a_compact_context_candidate import MAGIC


def compact_support(support):
    original = np.asarray(support['descriptors'])
    if original.dtype != np.float32 or original.ndim != 2:
        raise ValueError('Expected float32 descriptor matrix')
    if not np.isfinite(original).all():
        raise ValueError('Nonfinite descriptor matrix')
    rows = np.ascontiguousarray(original)
    _, first, inverse = np.unique(
        rows.view(np.dtype((np.void, rows.shape[1]*4))).reshape(-1),
        return_index=True, return_inverse=True)
    sequences = []
    for sequence in support['sequences']:
        indices = np.asarray(sequence, dtype=np.int64)
        if np.any(indices < 0) or np.any(indices >= len(rows)):
            raise ValueError('Invalid component index')
        sequences.append(inverse[indices].tolist())
    descriptors = rows[first]
    descriptors.setflags(write=False)
    return dict(support, descriptors=descriptors, sequences=sequences)


def decode_training_view(digest, payload):
    """Read existing SAB2 rows without expanding its deduplicated matrix."""
    if not payload.startswith(MAGIC):
        raise ValueError('Wrong codec')
    raw = zlib.decompress(payload[len(MAGIC):])
    if hashlib.sha256(raw).hexdigest() != digest:
        raise ValueError('SHA mismatch')
    data = json.loads(raw)
    if data['version'] != 2 or set(data['arrays']) != {'descriptors', 'history_features'}:
        raise ValueError('Schema mismatch')
    row_map = None
    for key, info in data['arrays'].items():
        if info['dtype'] != '<f4':
            raise ValueError('dtype')
        values = np.frombuffer(base64.b64decode(info['data'], validate=True), dtype='<f4')
        if key == 'descriptors':
            if len(info['shape']) != 2:
                raise ValueError('descriptor rank')
            values = values.reshape(info['row_count'], info['shape'][1])
            row_map = np.frombuffer(base64.b64decode(info['indices'], validate=True), dtype='<u4')
            if len(row_map) != info['shape'][0] or (len(row_map) and int(row_map.max()) >= len(values)):
                raise ValueError('indices')
        else:
            values = values.reshape(info['shape'])
        if not np.isfinite(values).all():
            raise ValueError('nonfinite')
        values.setflags(write=False)
        data['support'][key] = values
    sequences = []
    for sequence in data['support']['sequences']:
        indices = np.asarray(sequence, dtype=np.int64)
        if np.any(indices < 0) or np.any(indices >= len(row_map)):
            raise ValueError('Invalid component index')
        sequences.append(row_map[indices].tolist())
    data['support']['sequences'] = sequences
    data['support']['commands'] = tuple(data['support']['commands'])
    return dict(support=data['support'], document=data['document'])


class CompactTrainingViewMixin:
    """Only the bounded in-memory view changes, not journal bytes or IDs."""
    def remember_compact_prepared(self, context, value):
        # Trusted source-bound worker already produced the SAB2 compact layout.
        # Keep parent-side byte accounting, but do not deduplicate it again.
        return super()._remember(context, value)

    def _remember(self, context, value):
        compact = dict(value, support=compact_support(value['support']))
        return super()._remember(context, compact)

    def _read(self, context):
        context = int(context)
        if context in self.decoded:
            self.decoded_hits += 1
            self.decoded.move_to_end(context)
            return self.decoded[context][0]
        self.decoded_misses += 1
        row = self.db.execute('SELECT sha,payload FROM contexts WHERE id=?', (context,)).fetchone()
        if row is None:
            raise ValueError('Missing context')
        value = decode_training_view(row[0], bytes(row[1]))
        # Persisted rows are already unique. Avoid another np.unique here.
        return super()._remember(context, value)
