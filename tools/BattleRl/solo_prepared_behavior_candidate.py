"""Opt-in CPU preparation adapter around the unchanged recurrent behavior core."""
import time
from baseline_a_scenario_behavior import ScenarioBehavior


class PreparedBatch:
    def __init__(self, original_inputs, original_runtime, packets, context_ids):
        self.inputs, self.runtime, self.packets = original_inputs, original_runtime, packets
        self.context_ids = context_ids
        self.index = 0
        self.active = None

    def __getattr__(self, name):
        return getattr(self.inputs, name)

    def encode(self, native, history):
        if self.active is not None or self.index >= len(self.packets):
            raise ValueError('Prepared input consumed out of order')
        p = self.packets[self.index]
        if native['observation']['Step'] != p['step'] or native['support_sha256'] != p['native_support_sha256']:
            raise ValueError('Prepared state mismatch')
        self.active = p
        return p['observation'], p['support']

    def render(self, observation, support):
        if self.active is None or support is not self.active['support']:
            raise ValueError('Prepared candidate/document mismatch')
        return self.active['document']

    def add(self, support, document):
        p = self.active
        if p is None or support is not p['support'] or document is not p['document']:
            raise ValueError('Prepared journal consumed out of order')
        context = self.context_ids[self.index]
        self.active = None
        self.index += 1
        return context


class ParallelScenarioBehavior(ScenarioBehavior):
    def __init__(self, policy, inputs, runtime, renderer, prepare_pool):
        super().__init__(policy, inputs, runtime, renderer)
        self.prepare_pool = prepare_pool
        self.preparation_batch = 0

    def select_many(self, states, contexts):
        if not states or len(states) != len(contexts) or any(c['pending'] is not None for c in contexts):
            raise ValueError('Invalid CPU preparation batch')
        self.preparation_batch += 1
        started = time.perf_counter()
        packets = self.prepare_pool.prepare_many([
            ((self.preparation_batch, i), state, context['history'])
            for i, (state, context) in enumerate(zip(states, contexts))])
        context_ids = self.runtime.add_prepared_many(packets)
        seconds = time.perf_counter()-started
        originals = self.inputs, self.renderer, self.runtime
        adapter = PreparedBatch(self.inputs, self.runtime, packets, context_ids)
        self.inputs = self.renderer = self.runtime = adapter
        try:
            # policy.runtime remains bound to the original persistent journal.
            choices = super().select_many(states, contexts)
            if adapter.active is not None or adapter.index != len(states):
                raise ValueError('Incomplete preparation consumption')
            for choice in choices:
                choice['prepare_seconds'] += seconds/len(states)
            return choices
        finally:
            self.inputs, self.renderer, self.runtime = originals
