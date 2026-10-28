"""Produce unchanged SAB2 archive bytes and its compact cache view together."""
import base64
import hashlib
import json
import zlib
import numpy as np
from solo_a_compact_context_candidate import MAGIC


def encode_with_view(support, document):
    stored = dict(support)
    arrays = {}
    compact = dict(support)
    for key in ('descriptors', 'history_features'):
        value = np.asarray(stored.pop(key))
        if value.dtype != np.float32 or not np.isfinite(value).all():
            raise ValueError('Expected finite float32')
        value = value.astype('<f4', copy=False)
        info = dict(shape=list(value.shape), dtype='<f4')
        if key == 'descriptors':
            if value.ndim != 2:
                raise ValueError('Descriptor rank')
            unique, lookup, indices = [], {}, []
            for row in value:
                raw = row.tobytes()
                index = lookup.get(raw)
                if index is None:
                    index = len(unique)
                    lookup[raw] = index
                    unique.append(raw)
                indices.append(index)
            packed = b''.join(unique)
            info.update(data=base64.b64encode(packed).decode('ascii'),
                        row_count=len(unique),
                        indices=base64.b64encode(np.asarray(indices, dtype='<u4').tobytes()).decode('ascii'))
            compact[key] = np.frombuffer(packed, dtype='<f4').reshape(len(unique), value.shape[1])
            sequences = []
            for sequence in support['sequences']:
                if any(i < 0 or i >= len(indices) for i in sequence):
                    raise ValueError('Invalid component index')
                sequences.append([indices[i] for i in sequence])
            compact['sequences'] = sequences
        else:
            packed = value.tobytes()
            info['data'] = base64.b64encode(packed).decode('ascii')
            compact[key] = np.frombuffer(packed, dtype='<f4').reshape(value.shape)
        arrays[key] = info
    raw = json.dumps(dict(version=2, support=stored, arrays=arrays, document=document),
                     ensure_ascii=False, sort_keys=True, separators=(',', ':'), allow_nan=False).encode()
    return hashlib.sha256(raw).hexdigest(), MAGIC + zlib.compress(raw, 1), compact
