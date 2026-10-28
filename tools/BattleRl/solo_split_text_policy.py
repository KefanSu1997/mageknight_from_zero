"""Hierarchical policy with independently cached rules and short option text."""
import numpy as np
import torch
from baseline_a_scenario_policy import BaselineAScenarioPolicy, ScenarioDistribution
from structured_hierarchy_core import StructuredHierarchy
from solo_static_dynamic_fusion import StaticDynamicFusion


class SplitTextDistribution(ScenarioDistribution):
    def distributions(self, rows, nodes):
        vectors = self.runtime.vectors([(int(self.contexts[r]), int(n)) for r, n in zip(rows, nodes)])
        scores = []
        for row, node, vector in zip(rows, nodes, vectors):
            context = int(self.contexts[row])
            tree, _ = self.runtime.tree(context)
            if row not in self.cores:
                self.cores[row] = StructuredHierarchy(tree, self.flat[row])
            prior = self.cores[row].categorical(node).logits
            dynamic = torch.as_tensor(np.asarray(vector[1:]), device=self.latent.device,
                                      dtype=self.latent.dtype)
            if dynamic.shape != (len(prior), 384):
                raise ValueError('Incomplete dynamic option vectors')
            static = torch.as_tensor(self.runtime.rule_vectors(context), device=self.latent.device,
                                     dtype=self.latent.dtype)
            rules = static[None].expand(len(prior), -1, -1)
            present = torch.ones(rules.shape[:2], device=rules.device, dtype=torch.bool)
            residual = self.policy.split_fusion(self.latent[row:row+1].expand(len(prior), -1),
                                                dynamic, rules, present)
            scores.append(prior + residual)
        width = max(map(len, scores))
        return torch.distributions.Categorical(logits=torch.stack([
            torch.nn.functional.pad(s, (0, width-len(s)), value=-torch.inf) for s in scores]))


class SplitTextPolicy(BaselineAScenarioPolicy):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.split_fusion = StaticDynamicFusion()
        # The parent optimizer was constructed before the new trainable module.
        lr = self.optimizer.param_groups[0]['lr']
        self.optimizer = self.optimizer_class(self.parameters(), lr=lr, **self.optimizer_kwargs)

    def _get_action_dist_from_latent(self, latent_pi):
        supports = [self.runtime.support(int(c)) for c in self._contexts]
        return SplitTextDistribution(self, latent_pi, self.head.flat_logits(latent_pi, supports))
