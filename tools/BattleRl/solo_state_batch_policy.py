"""Undeployed full-state plus candidate batching, independent LSTM memories."""
from itertools import islice
from solo_ready_batch_policy import SoloReadyBatchPolicy
from solo_candidate_forest import state_contexts

class SoloStateBatchPolicy(SoloReadyBatchPolicy):
    def state_encoding(self,prepared):
        cached=getattr(self,'_batched_state',None)
        if cached is None:return super().state_encoding(prepared)
        original,encoded=cached
        if prepared is not original:raise ValueError('State batch identity mismatch')
        return encoded

    def step_many(self,prepared,memories):
        prepared=list(prepared);memories=list(memories)
        if len(prepared)!=len(memories):raise ValueError('Distinct memory per state required')
        if getattr(self,'_batched_state',None) is not None:raise ValueError('Reentrant state batch')
        encoded=state_contexts(self.structured,prepared)
        counts=[len(p['candidates']) for p in prepared]
        if not counts:return []
        roots=self.candidate_roots([r for p in prepared for r in p['candidates']])
        result=[]
        try:
            for p,m,s,c in zip(prepared,memories,encoded,roots.split(counts)):
                self._batched_state=(p,s);self._sequence_candidates=(p['candidates'],c)
                result.append(self.step(p,m))
            return result
        finally:
            self._batched_state=None;self._sequence_candidates=None

    def evaluate_sequence(self,prepared_steps,actions,memory=None,episode_starts=None):
        if getattr(self,'_batched_state',None) is not None:raise ValueError('Reentrant state sequence')
        iterator=iter(prepared_steps)
        def batched():
            while True:
                chunk=list(islice(iterator,8))
                if not chunk:return
                encoded=state_contexts(self.structured,chunk)
                counts=[len(p['candidates']) for p in chunk]
                self._sequence_candidates=None
                roots=self.candidate_roots([r for p in chunk for r in p['candidates']])
                for p,s,c in zip(chunk,encoded,roots.split(counts)):
                    yield dict(original=p,encoded=s,candidates=c)
        # Keep one bounded chunk and all its gradients until recurrent replay exits.
        from solo_structured_policy import SoloStructuredPolicy
        def prepared():
            for row in batched():
                self._batched_state=(row['original'],row['encoded'])
                self._sequence_candidates=(row['original']['candidates'],row['candidates'])
                yield row['original']
        try:
            return SoloStructuredPolicy.evaluate_sequence(self,prepared(),actions,memory,episode_starts)
        finally:
            self._batched_state=None;self._sequence_candidates=None
