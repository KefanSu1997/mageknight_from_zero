"""Exact legal-support categorical distribution, retaining global action IDs."""
import torch
from stable_baselines3.common.distributions import CategoricalDistribution
from sb3_battle_policy import BattleRecurrentPolicy


class GlobalLegalCategorical(CategoricalDistribution):
    def __init__(self, ids, valid, logits):
        super().__init__(logits.shape[1])
        self.ids, self.valid = ids, valid
        self.proba_distribution(logits.masked_fill(~valid, -torch.inf))

    def sample(self):
        local = super().sample()
        return self.ids.gather(1, local[:, None]).squeeze(1)

    def mode(self):
        local = super().mode()
        return self.ids.gather(1, local[:, None]).squeeze(1)

    def log_prob(self, actions):
        matches = (self.ids == actions.long().reshape(-1, 1)) & self.valid
        local = matches.long().argmax(1)
        return super().log_prob(local).masked_fill(~matches.any(1), -torch.inf)


class SparseBattleRecurrentPolicy(BattleRecurrentPolicy):
    def _get_action_dist_from_latent(self, latent_pi):
        # Sorted nonzero traversal preserves the global ordering and all legal
        # actions. Sequence-padding rows already have the original dummy ID 0.
        rows, global_ids = self._mask.nonzero(as_tuple=True)
        counts = self._mask.sum(1)
        width = int(counts.max().item())
        starts = counts.cumsum(0) - counts
        columns = torch.arange(len(rows), device=rows.device) - starts[rows]
        ids = torch.zeros((len(counts), width), dtype=torch.long, device=rows.device)
        valid = torch.zeros_like(ids, dtype=torch.bool)
        ids[rows, columns] = global_ids
        valid[rows, columns] = True
        # Encoding the union once avoids B*A*descriptor_width allocations.
        unique, inverse = torch.unique(global_ids, sorted=True, return_inverse=True)
        encoded = self.argument_encoder(self.descriptors[unique])
        query = self.action_net(latent_pi)
        scores = (query[rows] * encoded[inverse]).sum(-1)
        logits = query.new_zeros(ids.shape)
        logits[rows, columns] = scores
        return GlobalLegalCategorical(ids, valid, logits)
