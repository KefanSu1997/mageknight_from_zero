"""Isolated A-policy candidate: remove top-level operation cardinality prior.

This is a policy change, not an equivalent speed optimization. It must collect
new on-policy trajectories; old behavior data cannot be relabeled as this policy.
No rule, reward, legal leaf, recurrent state, or parameter tensor is changed.
"""
import numpy as np
import torch
from baseline_a_scenario_policy import BaselineAScenarioPolicy, ScenarioDistribution


def root_counts(tree, node):
    """Only the operation-Kind root gets a correction, never parameter choices."""
    children = tree.nodes[node]['children']
    if node != tree.branch(0) or not children or any(
            edge[0] != '完整操作/Kind' for edge in children):
        return None
    if not hasattr(tree, '_solo_operation_leaf_counts'):
        sizes = [0] * len(tree.nodes)
        for index in range(len(tree.nodes)-1, -1, -1):
            item = tree.nodes[index]
            sizes[index] = (1 if item['leaf'] is not None else
                            sum(sizes[c] for c in item['children'].values()))
        assert sizes[0] == tree.leaves
        tree._solo_operation_leaf_counts = sizes
    counts = [tree._solo_operation_leaf_counts[c] for c in children.values()]
    if not all(c > 0 for c in counts) or sum(counts) != tree.leaves:
        raise ValueError('Operation root must partition the full legal support')
    return counts


class CountNeutralScenarioDistribution(ScenarioDistribution):
    def distributions(self, rows, nodes):
        original = super().distributions(rows, nodes)
        corrections = torch.zeros_like(original.logits)
        for i, (row, node) in enumerate(zip(rows, nodes)):
            tree, _ = self.runtime.tree(int(self.contexts[row]))
            counts = root_counts(tree, node)
            if counts is not None:
                corrections[i, :len(counts)] = torch.log(
                    original.logits.new_tensor(counts))
        return torch.distributions.Categorical(logits=original.logits-corrections)


class CountNeutralScenarioPolicy(BaselineAScenarioPolicy):
    def _get_action_dist_from_latent(self, latent_pi):
        supports = [self.runtime.support(int(c)) for c in self._contexts]
        return CountNeutralScenarioDistribution(
            self, latent_pi, self.head.flat_logits(latent_pi, supports))
