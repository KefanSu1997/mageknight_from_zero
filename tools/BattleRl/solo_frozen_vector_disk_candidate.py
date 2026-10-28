"""Isolated content-addressed cache for a bound, frozen, single-document reader.

Never use this for trainable text encoders. The caller supplies the complete
encoder/precision/format contract; this cache does not change encoding batches.
"""
import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import numpy as np


class FrozenVectorDiskReader:
    def __init__(self, path, contract, factory):
        if not contract or contract.get('frozen') is not True:
            raise ValueError('Explicit frozen encoder contract required')
        self.path = Path(path)
        self.path.mkdir(parents=True, exist_ok=True)
        raw = json.dumps(contract, sort_keys=True, ensure_ascii=False).encode()
        self.contract_hash = hashlib.sha256(raw).hexdigest()
        binding = self.path / 'binding.json'
        if binding.exists() and binding.read_bytes() != raw:
            raise ValueError('Frozen encoder cache contract mismatch')
        if not binding.exists():
            binding.write_bytes(raw)
        self.factory = factory
        self.reader = None
        self.last_lengths = []
        self.hits = self.misses = 0

    def encode_many(self, documents, batch_size=None):
        if batch_size not in (None, 1):
            raise ValueError('Only bound single-document encoding is supported')
        documents = list(documents)
        result = []
        self.last_lengths = []
        for document in documents:
            raw = json.dumps(document, sort_keys=True, ensure_ascii=False,
                             separators=(',', ':'), allow_nan=False).encode()
            key = hashlib.sha256(self.contract_hash.encode() + b'\0' + raw).hexdigest()
            target = self.path / (key + '.bin')
            if target.exists():
                packet = target.read_bytes()
                digest, payload = packet[:32], packet[32:]
                if hashlib.sha256(payload).digest() != digest:
                    raise ValueError('Frozen vector cache checksum mismatch')
                value = np.load(io.BytesIO(payload), allow_pickle=False)
                self.hits += 1
                self.last_lengths.append(0)  # No token encoding on a cache hit.
            else:
                if self.reader is None:
                    self.reader = self.factory()
                values = self.reader.encode_many([document], batch_size=1)
                if len(values) != 1:
                    raise ValueError('Incomplete frozen encoding')
                value = values[0]
                self.last_lengths.append(sum(self.reader.last_lengths))
                self.misses += 1
                if value.dtype != np.float32 or value.shape != (len(document['options'])+1, 384) or not np.isfinite(value).all():
                    raise ValueError('Invalid frozen vector')
                stream = io.BytesIO()
                np.save(stream, value, allow_pickle=False)
                payload = stream.getvalue()
                if shutil.disk_usage(self.path).free < 3*2**30 + 2*len(payload):
                    raise RuntimeError('3GiB frozen cache disk protection')
                temporary = target.with_suffix('.tmp.' + str(os.getpid()))
                temporary.write_bytes(hashlib.sha256(payload).digest() + payload)
                os.replace(temporary, target)
            if value.dtype != np.float32 or value.shape != (len(document['options'])+1,384) or not np.isfinite(value).all():
                raise ValueError('Invalid cached frozen vector')
            value.setflags(write=False)
            result.append(value)
        return result
