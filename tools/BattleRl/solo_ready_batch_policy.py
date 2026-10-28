"""Isolated ready-environment candidate batching, without shared recurrent state."""
from solo_sequence_forest_policy import SoloSequenceForestPolicy


class SoloReadyBatchPolicy(SoloSequenceForestPolicy):
    def step_many(self, prepared, memories):
        prepared = list(prepared)
        memories = list(memories)
        if len(prepared) != len(memories):
            raise ValueError('One independent memory per environment is required')
        if getattr(self, '_sequence_candidates', None) is not None:
            raise RuntimeError('Reentrant batch evaluation is unsupported')
        if not prepared:
            return []
        counts = [len(p['candidates']) for p in prepared]
        roots = super().candidate_roots([c for p in prepared for c in p['candidates']])
        outputs = []
        try:
            for p, memory, part in zip(prepared, memories, roots.split(counts)):
                self._sequence_candidates = (p['candidates'], part)
                outputs.append(self.step(p, memory))
            return outputs
        finally:
            self._sequence_candidates = None
