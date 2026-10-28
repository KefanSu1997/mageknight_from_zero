"""Versioned lossless context codec candidate; live runtime does not use it."""
import base64
import hashlib
import json
import zlib
import numpy as np

MAGIC = b'SAB1\0'


def encode(support, document):
    stored = dict(support)
    arrays = {}
    for key in ('descriptors', 'history_features'):
        value = np.asarray(stored.pop(key))
        if value.dtype != np.float32 or not np.isfinite(value).all():
            raise ValueError('Expected finite float32 candidate arrays')
        arrays[key] = dict(shape=list(value.shape), dtype='<f4',
            data=base64.b64encode(value.astype('<f4', copy=False).tobytes(order='C')).decode('ascii'))
    raw = json.dumps(dict(version=1, support=stored, arrays=arrays, document=document),
        ensure_ascii=False, sort_keys=True, separators=(',', ':'), allow_nan=False).encode()
    return hashlib.sha256(raw).hexdigest(), MAGIC + zlib.compress(raw, 1)


def decode(digest, payload):
    if not payload.startswith(MAGIC):
        raise ValueError('Wrong candidate context format')
    raw = zlib.decompress(payload[len(MAGIC):])
    if hashlib.sha256(raw).hexdigest() != digest:
        raise ValueError('Context SHA mismatch')
    data = json.loads(raw)
    if data['version'] != 1:
        raise ValueError('Unsupported candidate context version')
    for key, array in data['arrays'].items():
        if key not in ('descriptors', 'history_features') or array['dtype'] != '<f4':
            raise ValueError('Unexpected candidate array')
        values = np.frombuffer(base64.b64decode(array['data'], validate=True), dtype='<f4')
        data['support'][key] = values.reshape(array['shape']).tolist()
    return dict(support=data['support'], document=data['document'])
