"""Staged entity encoder comparison; not yet used by the PPO trainer.

Caller supplies public entity features and a presence mask. No game state,
hidden audit fields, temporal attention or teacher actions are read here.
Multi-head self-attention, residual layer norms and feed-forward gradients
use the existing local NumPy runtime. GRU memory remains a separate concern.
"""
import numpy as np


class EntityTransformerBlock:
    def __init__(self, input_size, width=32, heads=4, feedforward=64, seed=7):
        if min(input_size, width, heads, feedforward) <= 0 or width % heads:
            raise ValueError('Positive dimensions and width divisible by heads required')
        self.input_size, self.width, self.heads = input_size, width, heads
        rng = np.random.default_rng(seed)
        def weight(a, b):
            return rng.normal(0, np.sqrt(2 / (a + b)), (a, b))
        self.parameters = dict(project=weight(input_size, width),
            q=weight(width, width), k=weight(width, width), v=weight(width, width),
            out=weight(width, width), ff1=weight(width, feedforward),
            ff1_bias=np.zeros(feedforward), ff2=weight(feedforward, width),
            ff2_bias=np.zeros(width), norm1_scale=np.ones(width),
            norm1_bias=np.zeros(width), norm2_scale=np.ones(width),
            norm2_bias=np.zeros(width))

    @staticmethod
    def _norm(x, scale, bias):
        center = x - x.mean(axis=-1, keepdims=True)
        inverse = 1 / np.sqrt((center * center).mean(axis=-1, keepdims=True) + 1e-5)
        normalized = center * inverse
        return normalized * scale + bias, (normalized, inverse)

    @staticmethod
    def _norm_backward(gradient, cache, scale):
        normalized, inverse = cache
        dx = gradient * scale
        dx = inverse * (dx - dx.mean(axis=-1, keepdims=True)
                        - normalized * (dx * normalized).mean(axis=-1, keepdims=True))
        return dx, (gradient * normalized).sum(axis=(0, 1)), gradient.sum(axis=(0, 1))

    def forward(self, features, present):
        x = np.asarray(features, dtype=np.float64)
        mask = np.asarray(present, dtype=bool)
        if x.ndim != 3 or x.shape[-1] != self.input_size or mask.shape != x.shape[:2]:
            raise ValueError('Features [batch,entities,input] and matching presence mask required')
        if not mask.any(axis=1).all() or not np.isfinite(x[mask]).all():
            raise ValueError('Every sample needs a finite public entity; use an explicit global token')
        # Ignore padded values, including NaN, before any matrix product.
        x = np.where(mask[..., None], x, 0.)
        p = self.parameters
        z = x @ p['project']
        batch, count, _ = z.shape
        depth = self.width // self.heads
        def heads(values):
            return values.reshape(batch, count, self.heads, depth).transpose(0, 2, 1, 3)
        q, k, v = (heads(z @ p[key]) for key in ('q', 'k', 'v'))
        scores = q @ k.transpose(0, 1, 3, 2) / np.sqrt(depth)
        scores = np.where(mask[:, None, None, :], scores, -np.inf)
        weights = np.exp(scores - scores.max(axis=-1, keepdims=True))
        weights /= weights.sum(axis=-1, keepdims=True)
        attention = (weights @ v).transpose(0, 2, 1, 3).reshape(batch, count, self.width)
        h, cache1 = self._norm(z + attention @ p['out'], p['norm1_scale'], p['norm1_bias'])
        f = np.tanh(h @ p['ff1'] + p['ff1_bias'])
        y, cache2 = self._norm(h + f @ p['ff2'] + p['ff2_bias'], p['norm2_scale'], p['norm2_bias'])
        pooling = mask[..., None] / mask.sum(axis=1)[:, None, None]
        pooled = (y * pooling).sum(axis=1)
        cache = (x, mask, z, q, k, v, weights, attention, h, f, cache1, cache2, pooling)
        return pooled, cache

    def backward(self, pooled_gradient, cache):
        x, mask, z, q, k, v, weights, attention, h, f, cache1, cache2, pooling = cache
        gradient = np.asarray(pooled_gradient, dtype=np.float64)
        if gradient.shape != (x.shape[0], self.width):
            raise ValueError('Pooled gradient dimensions differ')
        p = self.parameters
        g = {key: np.zeros_like(value) for key, value in p.items()}
        def outer(a, b):
            return a.reshape(-1, a.shape[-1]).T @ b.reshape(-1, b.shape[-1])
        dy = gradient[:, None, :] * pooling
        dr2, g['norm2_scale'], g['norm2_bias'] = self._norm_backward(dy, cache2, p['norm2_scale'])
        g['ff2'] = outer(f, dr2)
        g['ff2_bias'] = dr2.sum(axis=(0, 1))
        df = (dr2 @ p['ff2'].T) * (1 - f * f)
        g['ff1'] = outer(h, df)
        g['ff1_bias'] = df.sum(axis=(0, 1))
        dh = dr2 + df @ p['ff1'].T
        dr1, g['norm1_scale'], g['norm1_bias'] = self._norm_backward(dh, cache1, p['norm1_scale'])
        g['out'] = outer(attention, dr1)
        da = dr1 @ p['out'].T
        batch, count, _ = z.shape
        depth = self.width // self.heads
        da = da.reshape(batch, count, self.heads, depth).transpose(0, 2, 1, 3)
        dweights = da @ v.transpose(0, 1, 3, 2)
        dv = weights.transpose(0, 1, 3, 2) @ da
        ds = weights * (dweights - (dweights * weights).sum(axis=-1, keepdims=True))
        dq = ds @ k / np.sqrt(depth)
        dk = ds.transpose(0, 1, 3, 2) @ q / np.sqrt(depth)
        dz = dr1.copy()
        for key, derivative in (('q', dq), ('k', dk), ('v', dv)):
            derivative = derivative.transpose(0, 2, 1, 3).reshape(batch, count, self.width)
            g[key] = outer(z, derivative)
            dz += derivative @ p[key].T
        dz *= mask[..., None]
        g['project'] = outer(x, dz)
        dx = (dz @ p['project'].T) * mask[..., None]
        return dx, g
