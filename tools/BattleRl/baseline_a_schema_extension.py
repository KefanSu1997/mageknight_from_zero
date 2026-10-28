"""Explicit, auditable extension of A's numerical observation input.

This is an equivalence gate utility, not a training launcher. It preserves every
existing parameter and appends zero columns for new public scenario fields.
The new fields must still be independently audited for public information and
coverage. Optimizer migration and scenario policy acceptance are not implied.
"""
import hashlib
import json
import torch


NUMERIC_INPUTS = frozenset(
    prefix + '.numeric.0.weight' for prefix in
    ('features_extractor', 'pi_features_extractor', 'vf_features_extractor'))
BASELINE_WIDTH = 6411


def extend_state_dict(source, public_extension_names):
    names = tuple(public_extension_names)
    if not names or len(names) != len(set(names)):
        raise ValueError('Nonempty distinct public field names required')
    if any(not isinstance(n, str) or not n.startswith('world.') for n in names):
        raise ValueError('New fields require explicit world.* semantic names')
    if not NUMERIC_INPUTS.issubset(source):
        raise ValueError('Not the baseline A feature-extractor state dictionary')
    reference = source['features_extractor.numeric.0.weight']
    if reference.shape != (256, BASELINE_WIDTH):
        raise ValueError('Baseline A input width changed; do not prefix-guess')
    for key in NUMERIC_INPUTS:
        if not torch.equal(source[key], reference):
            raise ValueError('Shared actor/critic encoder aliases disagree')
    result = {}
    for key, value in source.items():
        if not torch.isfinite(value).all():
            raise ValueError('Nonfinite source parameter: '+key)
        result[key] = (torch.cat((value, value.new_zeros(256, len(names))), dim=1)
                       if key in NUMERIC_INPUTS else value.clone())
    schema = dict(version=1, original_width=BASELINE_WIDTH,
                  extension_names=names, final_width=BASELINE_WIDTH+len(names))
    schema['sha256'] = hashlib.sha256(json.dumps(schema, ensure_ascii=False,
        sort_keys=True, separators=(',', ':')).encode()).hexdigest()
    return result, schema


def audit_extension(source, extended, schema):
    if set(source) != set(extended):
        raise ValueError('Model topology changed beyond numerical input extension')
    for key in source:
        actual = extended[key]
        if key in NUMERIC_INPUTS:
            if actual.shape != (256, schema['final_width']):
                raise ValueError('Unexpected extended input shape')
            if not torch.equal(actual[:, :BASELINE_WIDTH], source[key]):
                raise ValueError('Existing public-coordinate weights changed')
            if torch.count_nonzero(actual[:, BASELINE_WIDTH:]):
                raise ValueError('Reference equivalence gate requires zero new columns')
        elif not torch.equal(source[key], actual):
            raise ValueError('Unrelated parameter changed: '+key)
    return dict(passed=True, tensors=len(source), changed_shapes=sorted(NUMERIC_INPUTS),
                scope='Exact parameter preservation; not environment or PPO acceptance')
