"""Isolated batching candidate; identical learned parameters and legal support."""
from solo_structured_policy import SoloStructuredPolicy
from solo_candidate_forest import roots

class SoloForestPolicy(SoloStructuredPolicy):
    def candidate_roots(self,candidates):
        return roots(self.structured,candidates)
