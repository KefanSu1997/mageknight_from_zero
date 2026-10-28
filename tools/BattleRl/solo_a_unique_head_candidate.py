"""Encode byte-identical component rows once; retain every candidate and weight."""
from collections import OrderedDict
import numpy as np
import torch

_cache = OrderedDict()
_bytes = 0
_limit = 64 * 2**20


def layout(support):
    global _bytes
    key = id(support)
    if key in _cache:
        _cache.move_to_end(key)
        return _cache[key][1]
    count = len(support['commands'])
    sizes = np.asarray([len(s) for s in support['sequences']], dtype=np.int64)
    if count < 1 or len(sizes) != count or np.any(sizes == 0):
        raise ValueError('Incomplete legal support')
    if len(support['weights']) != count or any(len(w) != n for w,n in zip(support['weights'],sizes)):
        raise ValueError('Missing component weights')
    indices = np.concatenate(support['sequences']).astype(np.int64, copy=False)
    original = np.asarray(support['descriptors'], dtype=np.float32)
    if np.any(indices < 0) or np.any(indices >= len(original)):
        raise ValueError('Component index outside support')
    used, inverse = np.unique(indices, return_inverse=True)
    rows = np.ascontiguousarray(original[used])
    if not np.isfinite(rows).all():
        raise ValueError('Nonfinite descriptors')
    _, first, row_inverse = np.unique(rows.view(np.dtype((np.void, rows.shape[1]*4))).reshape(-1),return_index=True,return_inverse=True)
    result = (rows[first],row_inverse[inverse],np.repeat(np.arange(count),sizes),
              np.concatenate(support['weights']).astype(np.float32,copy=False),count)
    # Include retained support arrays and container estimate in the cache budget.
    size = sum(x.nbytes for x in result[:-1]) + original.nbytes + len(indices)*32 + count*512
    if size <= _limit:
        _cache[key] = (support,result,size)
        _bytes += size
        while _bytes > _limit:
            _bytes -= _cache.popitem(last=False)[1][2]
    return result


def flat_logits(self, latent, supports):
    if latent.shape != (len(supports),128) or not supports:
        raise ValueError('Latent/support batch mismatch')
    parts = [layout(s) for s in supports]
    lengths = [p[4] for p in parts]
    row_offsets = np.cumsum([0]+[len(p[0]) for p in parts[:-1]])
    offsets = np.cumsum([0]+lengths[:-1])
    device = latent.device
    encoded = self.argument_encoder(torch.as_tensor(np.concatenate([p[0] for p in parts]),device=device))
    indices = torch.as_tensor(np.concatenate([p[1]+o for p,o in zip(parts,row_offsets)]),device=device)
    owners = torch.as_tensor(np.concatenate([p[2]+o for p,o in zip(parts,offsets)]),device=device)
    weights = torch.as_tensor(np.concatenate([p[3] for p in parts]),device=device)
    joint = encoded.new_zeros(sum(lengths),32).index_add(0,owners,encoded[indices]*weights[:,None])
    rows = torch.as_tensor(np.repeat(np.arange(len(parts)),lengths),device=device)
    scores = (self.action_net(latent)[rows]*joint).sum(-1)
    if not torch.isfinite(scores).all():
        raise ValueError('Nonfinite complete-action scores')
    return list(scores.split(lengths))
