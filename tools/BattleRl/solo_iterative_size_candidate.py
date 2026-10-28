"""Exact same retained-object accounting without recursive generators."""
import sys
import numpy as np


def retained_bytes(value):
    seen = set()
    stack = [value]
    total = 0
    while stack:
        item = stack.pop()
        identity = id(item)
        if identity in seen:
            continue
        seen.add(identity)
        total += sys.getsizeof(item)
        if isinstance(item, np.ndarray):
            if item.base is not None:
                total += item.nbytes
        elif isinstance(item, dict):
            stack.extend(item.keys())
            stack.extend(item.values())
        elif isinstance(item, (list, tuple)):
            stack.extend(item)
    return total
