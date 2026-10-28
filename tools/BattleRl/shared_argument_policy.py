"""Staged shared low-rank argument residual for the original masked PPO head.

Retains every original action column and kind gate. Only formal legal columns
receive a temporary residual during computation; saved parameters are restored
exactly, including exceptions. No kind-cardinality bias or action overrides.
"""
from contextlib import contextmanager
import numpy as np
from train_ppo import Policy


class SharedArgumentPolicy(Policy):
    def __init__(self, input_size, argument_features, rank=32, seed=7,
                 entropy_coefficient=.01, value_coefficient=.02):
        features = np.asarray(argument_features)
        if features.ndim != 2 or not np.isfinite(features).all() or rank <= 0:
            raise ValueError('Finite public catalog features and positive residual rank required')
        super().__init__(input_size, len(features), seed, entropy_coefficient, value_coefficient)
        self.argument_features = np.array(features, copy=True)
        self.argument_features.setflags(write=False)
        self.p['argument_query'] = np.random.default_rng(seed + 3).normal(0, .05, (64, rank))
        self.p['argument_project'] = np.zeros((features.shape[1], rank))
        self._residual_active = False

    @contextmanager
    def _residual(self, columns):
        if self._residual_active:
            raise RuntimeError('Nested residual scope is not supported')
        columns = np.asarray(columns, dtype=np.int64)
        matrix = self.p['wp']
        original = matrix[:, columns].copy()
        projected = self.argument_features[columns] @ self.p['argument_project']
        try:
            matrix[:, columns] = original + self.p['argument_query'] @ projected.T
            self._residual_active = True
            yield projected
        finally:
            matrix[:, columns] = original
            self._residual_active = False

    def forward(self, x, mask):
        if self._residual_active:
            return super().forward(x, mask)
        columns = np.flatnonzero(mask.any(axis=0))
        with self._residual(columns):
            return super().forward(x, mask)

    def action_forward(self, features, legal):
        with self._residual(legal):
            return super().action_forward(features, legal)

    def loss_grad(self, *args, **kwargs):
        if not self._residual_active:
            raise ValueError('Shared residual PPO must use the formal legal-union loss entry')
        return super().loss_grad(*args, **kwargs)

    def update(self, *args, **kwargs):
        raise ValueError('Use the complete recurrent Adam checkpoint optimizer, not the legacy head-only optimizer')

    def loss_grad_legal(self, x, mask, a, oldlog, adv, returns, return_input_gradient=False):
        columns = np.flatnonzero(mask.any(axis=0))
        with self._residual(columns) as projected:
            result = super().loss_grad_legal(x, mask, a, oldlog, adv, returns, return_input_gradient)
        loss, gradients, kl = result[:3]
        effective_gradient = gradients['wp'][:, columns]
        gradients['argument_query'] = effective_gradient @ projected
        gradients['argument_project'] = self.argument_features[columns].T @ effective_gradient.T @ self.p['argument_query']
        return (loss, gradients, kl, result[3]) if return_input_gradient else (loss, gradients, kl)

    def rollout_kl_legal(self, x, mask, a, oldlog):
        columns = np.flatnonzero(mask.any(axis=0))
        with self._residual(columns):
            return super().rollout_kl_legal(x, mask, a, oldlog)
