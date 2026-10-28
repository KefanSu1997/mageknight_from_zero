"""Undeployed policy variant; collection and recurrent PPO share step()."""
from solo_forest_policy import SoloForestPolicy
from solo_group_prior import count_neutral_logits

class SoloGroupPriorPolicy(SoloForestPolicy):
    def step(self,prepared,memory=None,episode_start=False):
        logits,value,memory=super().step(prepared,memory,episode_start)
        return count_neutral_logits(logits,prepared['commands']),value,memory
