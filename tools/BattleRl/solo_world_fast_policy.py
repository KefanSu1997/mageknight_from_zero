"""World expert: A's full structured/text candidate scorer, no online reader.

All candidate components retain their pretrained printed-rule vectors. This is
an explicit new expert design, not probability-equivalence to contextual A.
"""
import torch
from baseline_a_scenario_policy import BaselineAScenarioPolicy


class CompleteWorldDistribution:
    def __init__(self, logits):
        self.distribution = torch.distributions.Categorical(logits=logits)

    def sample_rows(self, generators=None, deterministic=False):
        if deterministic:
            return self.distribution.logits.argmax(-1)
        if generators is None:
            return self.distribution.sample()
        if len(generators) != len(self.distribution.probs):
            raise ValueError('One independent RNG per active task required')
        return torch.stack([torch.multinomial(p, 1, generator=g)[0]
                            for p, g in zip(self.distribution.probs, generators)])

    def get_actions(self, deterministic=False):
        return self.sample_rows(deterministic=deterministic)

    def sample(self):
        return self.sample_rows()

    def mode(self):
        return self.sample_rows(deterministic=True)

    def log_prob(self, actions):
        return self.distribution.log_prob(actions.reshape(-1).long())

    def entropy(self):
        # The original A training hook supplies its importance-weighted sampled
        # entropy; preserve that estimator rather than silently changing PPO.
        return None


class FastWorldPolicy(BaselineAScenarioPolicy):
    def _get_action_dist_from_latent(self, latent_pi):
        supports = [self.runtime.support(int(c)) for c in self._contexts]
        flat = self.head.flat_logits(latent_pi, supports)
        if len(flat) != len(supports):
            raise ValueError('Incomplete world candidate batch')
        width = max(len(s['commands']) for s in supports)
        rows = []
        for scores, support in zip(flat, supports):
            count = len(support['commands'])
            if len(scores) != count or not torch.isfinite(scores).all():
                raise ValueError('Incomplete/nonfinite world support scores')
            rows.append(torch.nn.functional.pad(scores, (0, width-count), value=-torch.inf))
        return CompleteWorldDistribution(torch.stack(rows))
