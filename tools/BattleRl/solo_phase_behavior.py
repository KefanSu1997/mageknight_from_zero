"""Batch public-phase decisions through separate A expert policies."""
import hashlib
import copy
from pathlib import Path
import numpy as np
import torch
from solo_phase_routing import route


class PhaseBehavior:
    def __init__(self, experts):
        if set(experts) != {'world', 'combat'}:
            raise ValueError('Both independent phase experts are required')
        self.experts = experts

    def begin(self, seed):
        children = {name: expert.begin(seed) for name, expert in self.experts.items()}
        # A single task-local random stream survives phase switches. Memories
        # remain separate and are updated only when their expert is selected.
        children['combat']['generator'] = children['world']['generator']
        return dict(experts=children, history=np.zeros(97, np.float32), rows=[], pending=None)

    def select(self, state, context):
        return self.select_many([state], [context])[0]

    def select_many(self, states, contexts):
        if not states or len(states) != len(contexts):
            raise ValueError('Phase batch mismatch')
        if len({id(c) for c in contexts}) != len(contexts):
            raise ValueError('An environment cannot appear twice in a decision batch')
        groups = {'world': [], 'combat': []}
        for i, (state, context) in enumerate(zip(states, contexts)):
            if context['pending'] is not None:
                raise ValueError('Previous native transition is still pending')
            name = route(state['observation'])
            if name == 'terminal':
                raise ValueError('Terminal has no policy action')
            groups[name].append(i)
        choices = [None] * len(states)
        for name, indices in groups.items():
            if not indices:
                continue
            children = [contexts[i]['experts'][name] for i in indices]
            for i, child in zip(indices, children):
                child['history'] = contexts[i]['history'].copy()
            batch = self.experts[name].select_many([states[i] for i in indices], children)
            if len(batch) != len(indices):
                raise ValueError('Expert returned an incomplete batch')
            for i, choice in zip(indices, batch):
                choices[i] = choice
                contexts[i]['pending'] = name
        return choices

    def transition(self, result, context):
        name = context['pending']
        if name not in self.experts:
            raise ValueError('Unexpected native transition')
        child = context['experts'][name]
        self.experts[name].transition(result, child)
        row = child['rows'][-1]
        row['expert'] = name
        context['rows'].append(row)
        context['history'] = child['history'].copy()
        context['pending'] = None

    def snapshot(self, context):
        return dict(version='phase_behavior_v1',
                    experts={name: self.experts[name].snapshot(child)
                             for name, child in context['experts'].items()},
                    history=context['history'].copy(), rows=context['rows'],
                    pending=context['pending'])

    def restore(self, snapshot):
        if snapshot.get('version') != 'phase_behavior_v1':
            raise ValueError('Unsupported phase behavior snapshot')
        saved = copy.deepcopy(snapshot)
        if set(saved['experts']) != set(self.experts):
            raise ValueError('Incomplete expert snapshot')
        states = [saved['experts'][n]['generator'] for n in ('world', 'combat')]
        if not torch.equal(states[0], states[1]):
            raise ValueError('Task random stream differs across expert snapshots')
        restored = self.begin(0)
        generator = restored['experts']['world']['generator']
        generator.set_state(states[0].cpu())
        for name, expert in self.experts.items():
            child = saved['experts'][name]
            device = expert.policy.device
            memory = child['memory']
            child['memory'] = type(memory)(*(tuple(t.to(device) for t in pair) for pair in memory))
            child['generator'] = generator
            child.pop('generator_device', None)
            restored['experts'][name] = child
        restored.update(history=saved['history'], rows=saved['rows'], pending=saved['pending'])
        pending_children = [name for name, c in restored['experts'].items() if c['pending'] is not None]
        if pending_children != ([] if restored['pending'] is None else [restored['pending']]):
            raise ValueError('Pending native transition/phase mismatch')
        return restored

    def save_policy(self, path):
        path = Path(path)
        bindings = {name: expert.save_policy(path.with_name(path.stem+'_'+name+'.pt'))
                    for name, expert in self.experts.items()}
        torch.save(dict(version='phase_behavior_v1', experts=bindings), path)
        return dict(kind='phase_behavior_v1', experts=bindings,
                    sha256=hashlib.sha256(path.read_bytes()).hexdigest())
