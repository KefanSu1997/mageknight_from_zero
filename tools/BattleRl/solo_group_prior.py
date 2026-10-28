"""Experimental count-neutral prior over operation families, not deployed.

Each family is the command's one active top-level operation plus its explicit
Kind, if present. Subtract log(number of legal parameterizations) from each
leaf logit. This preserves every leaf and learns arbitrary family preferences;
it is equivalent to family log-mean-exp followed by conditional leaf softmax.
Grouping is a policy-design choice, not a claim about equivalent game actions.
"""
from collections import Counter
import json
import math
import torch
from solo_candidate_entities import SUPPORTED_OPERATIONS

def family(command):
    value=json.loads(command) if isinstance(command,str) else command
    fields=[k for k,v in value.items() if v is not None and k not in ('SchemaVersion','ExpectedStep')]
    if len(fields)!=1 or fields[0] not in SUPPORTED_OPERATIONS:
        raise ValueError('Unknown or non-atomic operation family')
    name=fields[0];body=value[name]
    return (name,body.get('Kind') if isinstance(body,dict) else None)

def count_neutral_logits(logits,commands):
    if logits.ndim!=1 or len(commands)!=len(logits) or not len(commands):
        raise ValueError('Full nonempty candidate vector required')
    keys=[family(c) for c in commands];counts=Counter(keys)
    offset=logits.new_tensor([math.log(counts[k]) for k in keys])
    return logits-offset
