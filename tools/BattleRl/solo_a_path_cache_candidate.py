"""Reuse immutable tree topology and sampled ragged log probabilities for A."""
import torch

from structured_hierarchy_core import StructuredHierarchy


class CachedHierarchy(StructuredHierarchy):
    def __init__(self, tree, leaf_logits, residual=None):
        if leaf_logits.ndim != 1 or len(leaf_logits) != tree.leaves:
            raise ValueError('Every original complete candidate needs one score')
        if not torch.isfinite(leaf_logits).all():
            raise ValueError('Nonfinite leaf scores')
        self.tree = tree
        self.logits = leaf_logits
        self.residual = residual
        self.cache = {}
        descendants = getattr(tree, '_a_cached_descendants', None)
        if descendants is None:
            descendants = [None] * len(tree.nodes)
            for node in reversed(range(len(tree.nodes))):
                data = tree.nodes[node]
                descendants[node] = ([data['leaf']] if data['leaf'] is not None else
                    [leaf for child in data['children'].values() for leaf in descendants[child]])
            if sorted(descendants[0]) != list(range(tree.leaves)):
                raise ValueError('Tree must preserve every complete candidate exactly once')
            tree._a_cached_descendants = descendants
        self.descendants = descendants


def log_prob(self, actions):
    from final_token_policy import Distribution
    rows, nodes, choices = [], [], []
    for row, (context, action) in enumerate(zip(self.contexts, actions.detach().cpu().numpy().reshape(-1))):
        if not context:
            continue
        tree, _ = self.runtime.tree(int(context))
        if not 0 <= int(action) < tree.leaves:
            raise ValueError('Action outside complete support')
        for node, edge in tree.paths[int(action)]:
            children = tree.nodes[node]['children']
            if len(children) > 1:
                rows.append(row)
                nodes.append(node)
                choices.append(list(children).index(edge))
    if not rows:
        return self.latent.sum(-1) * 0
    if all((r, n) in self.memo for r, n in zip(rows, nodes)):
        # Memoized rows have different padded widths. Select the scalar first;
        # stacking entire rows incorrectly requires the same branch width.
        terms = torch.stack([self.memo[(r, n)][c] for r, n, c in zip(rows, nodes, choices)])
        return (self.latent.sum(-1)*0).index_add(0, torch.tensor(rows, device=terms.device), terms)
    self.memo.clear()
    return Distribution.log_prob(self, actions)


def install():
    import baseline_a_scenario_policy as policy
    policy.StructuredHierarchy = CachedHierarchy
    policy.ScenarioDistribution.log_prob = log_prob
