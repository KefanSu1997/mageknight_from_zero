"""Bind immutable rollout likelihoods while upstream PPO optimizes the green successor.

Independent of retired blue policy classes. This module does not change PPO loss,
rewards, supports, or optimizer behavior.
"""
from contextlib import contextmanager
import numpy as np


def rollout_reference(buffer):
    contexts = np.asarray(buffer.observations['context_id']).reshape(-1)
    actions = np.asarray(buffer.actions).reshape(-1)
    log_probs = np.asarray(buffer.log_probs).reshape(-1)
    if not (len(contexts) == len(actions) == len(log_probs)):
        raise ValueError('Rollout context/action/likelihood lengths differ')
    if not (np.isfinite(contexts).all() and np.isfinite(actions).all()
            and np.isfinite(log_probs).all()):
        raise ValueError('Nonfinite rollout entropy reference')
    if np.any(contexts != np.floor(contexts)) or np.any(actions != np.floor(actions)):
        raise ValueError('Rollout identities must be exact integers')
    rows = {}
    for context, action, likelihood in zip(contexts, actions, log_probs):
        if context == 0:
            continue  # Upstream recurrent padding has no real context.
        if context < 0 or action < 0:
            raise ValueError('Negative rollout identity')
        key = (int(context), int(action))
        likelihood = float(likelihood)
        if key in rows and abs(rows[key] - likelihood) > 1e-6:
            raise ValueError('Context/action reused with different behavior likelihoods')
        rows[key] = likelihood
    if not rows:
        raise ValueError('Empty rollout entropy reference')
    return rows


@contextmanager
def bound_rollout_entropy(model):
    """Wrap one upstream train() call; always release the rollout reference."""
    policy = model.policy
    if getattr(policy, '_entropy_reference', None) is not None:
        raise RuntimeError('Rollout entropy reference already bound')
    policy._entropy_reference = rollout_reference(model.rollout_buffer)
    try:
        yield
    finally:
        policy._entropy_reference = None
