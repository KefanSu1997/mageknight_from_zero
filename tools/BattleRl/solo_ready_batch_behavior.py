"""Isolated batching adapter; task-local sampling RNG is preserved."""
import time
import torch
from solo_episode_collector import SoloBehavior


class SoloReadyBatchBehavior(SoloBehavior):
    def select_many(self, states, contexts):
        states, contexts = list(states), list(contexts)
        if len(states) != len(contexts) or len({id(c) for c in contexts}) != len(contexts):
            raise ValueError('Distinct context required for every state')
        if not states:
            return []
        start = time.perf_counter()
        prepared = [self.builder.prepare(state) for state in states]
        prepare = time.perf_counter() - start
        start = time.perf_counter()
        with torch.inference_mode():
            outputs = self.model.step_many(prepared, [c['memory'] for c in contexts])
            logs = [torch.log_softmax(logits, 0) for logits, _, _ in outputs]
            lengths = [x.numel() for x in logs]
            # One transfer includes all legal probabilities and state values.
            packed = torch.cat([*logs, torch.stack([v.reshape(()) for _, v, _ in outputs])]).cpu()
            if not torch.isfinite(packed).all():
                raise ValueError('Nonfinite behavior distribution/value')
            cpu_logs = packed[:-len(outputs)].split(lengths)
            values = packed[-len(outputs):]
            choices = []
            for logp, value, context, (_, _, memory) in zip(cpu_logs, values, contexts, outputs):
                index = int(torch.multinomial(logp.exp(), 1, generator=context['generator']))
                choices.append(dict(index=index, log_probability=float(logp[index]), value=float(value)))
                context['memory'] = memory
        elapsed = time.perf_counter() - start
        for choice in choices:
            choice.update(prepare_seconds=prepare / len(choices), inference_seconds=elapsed / len(choices),
                          inference_batch_size=len(choices), timing_allocation='equal_share_of_batch_wall')
        return choices
