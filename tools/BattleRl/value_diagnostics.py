"""Critic diagnostics against fixed, unnormalized GAE return targets."""
import numpy as np


def measure(predictions, targets):
    predictions = np.asarray(predictions, dtype=np.float64).ravel()
    targets = np.asarray(targets, dtype=np.float64).ravel()
    if predictions.shape != targets.shape or not len(targets):
        raise ValueError('Value diagnostics require matching nonempty samples')
    if not np.isfinite(predictions).all() or not np.isfinite(targets).all():
        raise ValueError('Nonfinite critic diagnostic inputs')
    error = targets - predictions
    variance = float(np.var(targets))
    return dict(explained_variance=None if variance <= 1e-12 else float(1-np.var(error)/variance),
                target_variance=variance, mse=float(np.mean(error**2)),
                bias=float(np.mean(predictions-targets)), samples=len(targets))
