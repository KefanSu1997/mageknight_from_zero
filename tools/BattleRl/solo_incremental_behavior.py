"""Episode-owned projection caches around the existing batched collector."""
from baseline_a_scenario_behavior import ScenarioBehavior
from solo_incremental_inputs import for_episode


class _BatchInputs:
    def __init__(self, inputs, contexts):
        self.inputs, self.contexts = inputs, contexts
        self.index = 0

    def __getattr__(self, key):
        return getattr(self.inputs, key)

    def encode(self, state, history):
        if self.index >= len(self.contexts):
            raise ValueError('Input preparation exceeded scheduled environments')
        context = self.contexts[self.index]
        self.index += 1
        if '_incremental_inputs' not in context:
            context['_incremental_inputs'] = for_episode(self.inputs)
        return context['_incremental_inputs'].encode(state, history)


class IncrementalScenarioBehavior(ScenarioBehavior):
    def select_many(self, states, contexts):
        original = self.inputs
        adapter = _BatchInputs(original, contexts)
        self.inputs = adapter
        try:
            choices = super().select_many(states, contexts)
            if adapter.index != len(contexts):
                raise ValueError('Input preparation missed scheduled environments')
            return choices
        finally:
            self.inputs = original

    # Parent begin() creates new episode context, dropping all old dependencies.
    # Parent snapshot() saves model/RNG/history/rows explicitly, excluding caches.
    # A restored episode recomputes projections on its first actual observation.
