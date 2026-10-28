"""Equivalent descriptor block assembly, avoiding one ndarray view per row.

No observation/binding cache, no command pruning, no model change. Opt-in only.
"""
import hashlib
import json
import numpy as np
from solo_a_command_compile_v2_candidate import canonical, _compiled, candidate_entities

def prepare_support(observation,commands,schema,text_assets):
    schema.validate(observation,'observation')
    if not commands:raise ValueError('Empty live support')
    sequences=[];descriptors=[];weights=[];operations=[];bindings=[];history=[];offset=0
    for encoded in commands:
        command=json.loads(encoded);schema.validate(command,'command')
        components,numeric=_compiled(command)
        refs=candidate_entities(observation,command)
        texts=text_assets.encode_bindings(refs)
        vectors=[v for item in texts for v in item['vectors']]
        # Only known printed rules contribute. Structural parameters without a
        # printed identity carry no invented text vector; contextual branch
        # descriptions are supplied separately by the original A residual.
        full=np.zeros((len(components),480),np.float32)
        full[:,:96]=numeric
        if vectors:full[-1,96:]=np.stack(vectors).mean(0)
        history.append(numeric.mean(axis=0))
        if not np.isfinite(full).all():raise ValueError('Nonfinite candidate descriptor')
        start=offset;offset+=len(components);descriptors.append(full)
        sequences.append(list(range(start,start+len(components))))
        weights.append([.5/(len(components)-1)]*(len(components)-1)+[1.])
        operations.append([c for c,_ in components])
        bindings.append([dict(parameter=t['parameter'],identities=t['identities']) for t in texts])
    descriptor=np.concatenate(descriptors,axis=0);descriptor.setflags(write=False)
    return dict(commands=tuple(commands),sequences=sequences,descriptors=descriptor,weights=weights,
                history_features=np.asarray(history,np.float32),
                operations=operations,rule_bindings=bindings,
                commands_sha256=hashlib.sha256(canonical(list(commands)).encode()).hexdigest(),
                descriptor_schema='baseline_A_numeric96_rule384_solo_parameters_v1',
                status='candidate_inputs_only_contextual_residual_required')


