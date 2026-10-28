"""Isolated cross-time candidate batching; recurrent history stays sequential."""
from itertools import islice
from solo_vectorized_text_policy import SoloVectorizedTextPolicy


class SoloSequenceForestPolicy(SoloVectorizedTextPolicy):
    def candidate_roots(self, candidates):
        cached = getattr(self, '_sequence_candidates', None)
        if cached is None:
            return super().candidate_roots(candidates)
        original, roots = cached
        if candidates is not original:
            raise ValueError('Cross-time candidate order mismatch')
        return roots

    def evaluate_sequence(self, prepared_steps, actions, memory=None, episode_starts=None):
        if getattr(self, '_sequence_candidates', None) is not None:
            raise RuntimeError('Reentrant sequence evaluation is unsupported')
        iterator = iter(prepared_steps)

        def batched():
            while True:
                chunk = list(islice(iterator, 8))
                if not chunk:
                    return
                counts = [len(p['candidates']) for p in chunk]
                flat = [candidate for p in chunk for candidate in p['candidates']]
                # Same independent tree roots; no state/history or action pruning.
                roots = super(SoloSequenceForestPolicy, self).candidate_roots(flat)
                for prepared, part in zip(chunk, roots.split(counts)):
                    self._sequence_candidates = (prepared['candidates'], part)
                    yield prepared
                self._sequence_candidates = None

        try:
            return super().evaluate_sequence(batched(), actions, memory, episode_starts)
        finally:
            self._sequence_candidates = None
