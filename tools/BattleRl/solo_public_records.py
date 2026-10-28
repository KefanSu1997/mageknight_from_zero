"""Lossless public input records for the solo A-policy adapter.

No fixed battle slot limits, hashed feature collisions, hidden journal fields,
or implicit truncation. Neural projection/text encoding is a separate stage.
"""
import json
import math

PUBLIC_ROOT = frozenset(('Version', 'Step', 'Terminal', 'World', 'Inventory', 'Combat', 'Offers'))
PRIVATE_KEYS = frozenset(('PrivateStateBefore', 'PrivateStateAfter', 'Seeds',
    'RandomState', 'initial_state_sha256', 'current_state_sha256', 'records', 'setup'))


def flatten(value, path=()):
    """Record containers too, so empty lists/nulls/order remain distinguishable."""
    if isinstance(value, dict):
        if any(not isinstance(k, str) or k in PRIVATE_KEYS for k in value):
            raise ValueError('Private or non-string field in policy input')
        yield (path, 'object', tuple(sorted(value)))
        for key in sorted(value):
            yield from flatten(value[key], path+(key,))
    elif isinstance(value, list):
        yield (path, 'array', len(value))
        for index, item in enumerate(value):
            yield from flatten(item, path+(index,))
    elif value is None:
        yield (path, 'null', None)
    elif type(value) is bool:
        yield (path, 'bool', value)
    elif type(value) in (int, float):
        if not math.isfinite(value):
            raise ValueError('Nonfinite public numeric input')
        yield (path, 'number', value)
    elif isinstance(value, str):
        yield (path, 'text', value)
    else:
        raise TypeError(type(value).__name__)


def restore(records):
    iterator = iter(records)
    def read(path):
        actual, kind, value = next(iterator)
        if tuple(actual) != path:
            raise ValueError('Broken public record path')
        if kind == 'object':
            return {key: read(path+(key,)) for key in value}
        if kind == 'array':
            return [read(path+(i,)) for i in range(value)]
        if kind not in ('null', 'bool', 'number', 'text'):
            raise ValueError('Unknown record kind')
        return value
    result = read(())
    if next(iterator, None) is not None:
        raise ValueError('Trailing records')
    return result


def policy_records(native_state):
    observation = native_state['observation']
    if set(observation) != PUBLIC_ROOT or observation['Version'] != 1:
        raise ValueError('Public observation schema requires explicit migration')
    # Step is a transport anti-staleness counter, not a game resource or clock.
    public = {key: value for key, value in observation.items() if key != 'Step'}
    state = tuple(flatten(public))
    choices = []
    for encoded in native_state['commands']:
        command = json.loads(encoded)
        if command.get('SchemaVersion') != 1 or command.get('ExpectedStep') != observation['Step']:
            raise ValueError('Stale or unsupported candidate')
        payload = {k: v for k, v in command.items()
                   if k not in ('SchemaVersion', 'ExpectedStep') and v is not None}
        if len(payload) != 1:
            raise ValueError('Candidate must have exactly one complete operation')
        choices.append(tuple(flatten(payload)))
    if not choices and not observation['Terminal']:
        raise ValueError('Nonterminal state has no implemented legal action')
    return dict(state=state, candidates=tuple(choices),
                support_sha256=native_state['support_sha256'])
