"""Stream NumPy buffers without pickle's full-array temporary byte copies."""
import gzip
import pickle
from pathlib import Path
import numpy as np


def save(buffer, destination):
    destination = Path(destination)
    pending = destination.with_name(destination.name + '.pending')
    pending.mkdir(exist_ok=False)
    arrays = {}

    class Writer(pickle.Pickler):
        def persistent_id(self, obj):
            if not isinstance(obj, np.ndarray):
                return None
            key = arrays.get(id(obj))
            if key is None:
                key = f'array_{len(arrays):04d}.npy.gz'
                arrays[id(obj)] = key
                with gzip.open(pending / key, 'wb', compresslevel=1) as f:
                    np.lib.format.write_array(f, obj, allow_pickle=False)
            return ('numpy', key)

    with gzip.open(pending / 'metadata.pkl.gz', 'wb', compresslevel=1) as f:
        Writer(f, protocol=5).dump(buffer)
    pending.rename(destination)


def load(destination):
    destination = Path(destination)
    arrays = {}

    class Reader(pickle.Unpickler):
        def persistent_load(self, ref):
            kind, key = ref
            if kind != 'numpy' or Path(key).name != key:
                raise ValueError('Invalid array reference')
            if key not in arrays:
                with gzip.open(destination / key, 'rb') as f:
                    arrays[key] = np.load(f, allow_pickle=False)
            return arrays[key]

    with gzip.open(destination / 'metadata.pkl.gz', 'rb') as f:
        return Reader(f).load()
