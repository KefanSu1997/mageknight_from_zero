"""Same A candidate sums, packed once per immutable support rather than per PPO pass."""
from collections import OrderedDict

import numpy as np
import torch


_layouts = OrderedDict()
_bytes = 0
_limit = 64*2**20


def layout(support):
    global _bytes
    key = id(support)
    if key in _layouts:
        _layouts.move_to_end(key)
        return _layouts[key][1]
    count = len(support['commands'])
    if count < 1 or len(support['sequences']) != count or len(support['weights']) != count:
        raise ValueError('Invalid complete support')
    sizes = np.fromiter((len(s) for s in support['sequences']), dtype=np.int64, count=count)
    if np.any(sizes == 0) or any(len(w) != n for w, n in zip(support['weights'], sizes)):
        raise ValueError('Missing operation components')
    indices = np.concatenate(support['sequences']).astype(np.int64, copy=False)
    descriptors = support['descriptors'][indices]
    owners = np.repeat(np.arange(count, dtype=np.int64), sizes)
    weights = np.concatenate(support['weights']).astype(np.float32, copy=False)
    result = (descriptors, owners, weights, count)
    return result


def flat_logits(self, latent, supports):
    if latent.shape != (len(supports), 128) or not supports:
        raise ValueError('Latent/support batch mismatch')
    parts = [layout(s) for s in supports]
    lengths = [p[3] for p in parts]
    offsets = np.cumsum([0] + lengths[:-1])
    descriptors = np.concatenate([p[0] for p in parts])
    owners = np.concatenate([p[1] + offset for p, offset in zip(parts, offsets)])
    weights = np.concatenate([p[2] for p in parts])
    rows = np.repeat(np.arange(len(parts), dtype=np.int64), lengths)
    device = latent.device
    encoded = self.argument_encoder(torch.as_tensor(descriptors, device=device))
    joint = encoded.new_zeros(sum(lengths), 32).index_add(
        0, torch.as_tensor(owners, device=device),
        encoded * torch.as_tensor(weights, device=device)[:, None])
    scores = (self.action_net(latent)[torch.as_tensor(rows, device=device)] * joint).sum(-1)
    if not torch.isfinite(scores).all():
        raise ValueError('Nonfinite complete-action scores')
    return list(scores.split(lengths))

