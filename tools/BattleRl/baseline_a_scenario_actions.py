"""Complete solo commands -> A's shared 96-numeric + 384-rule components.

Retains every command and every parameter. A's signed-label compression is
preserved; it is not a claim that 96 coordinates are a lossless serialization.
The exact commands remain the immutable action identities in the journal.
"""
import hashlib
import json
import math
import numpy as np
from action_argument_features import catalog_argument_features
from solo_policy_inputs import candidate_entities


def canonical(value):
    return json.dumps(value,ensure_ascii=False,sort_keys=True,separators=(',',':'),allow_nan=False)


def parameter_components(command):
    """Explicit structure, indices and typed leaves; no graph neural network."""
    result=[]
    def walk(value,path):
        name='/'.join(map(str,path))
        if isinstance(value,dict):
            result.append((dict(Kind='parameter',Choice='object',Input=name),None))
            for key,item in sorted(value.items()):walk(item,(*path,key))
        elif isinstance(value,list):
            result.append((dict(Kind='parameter',Choice='array',Input=name),len(value)))
            for i,item in enumerate(value):walk(item,(*path,i))
        else:
            kind='null' if value is None else 'bool' if type(value) is bool else 'number' if type(value) in (int,float) else 'text'
            if kind=='text' and not isinstance(value,str):raise ValueError('Unsupported command value')
            result.append((dict(Kind='parameter',Choice=kind,Input=name+'='+canonical(value)),
                           value if kind in ('bool','number') else None))
    active={k:v for k,v in command.items() if k not in ('SchemaVersion','ExpectedStep') and v is not None}
    if len(active)!=1:raise ValueError('Exactly one complete native operation required')
    family=next(iter(active))
    walk(active,())
    # Same final-consumer weighting as A; this is one complete native commit.
    result.append((dict(Kind=family,Choice='complete',Input=canonical(active)),None))
    return result


def prepare_support(observation,commands,schema,text_assets):
    schema.validate(observation,'observation')
    if not commands:raise ValueError('Empty live support')
    sequences=[];descriptors=[];weights=[];operations=[];bindings=[];history=[]
    for encoded in commands:
        command=json.loads(encoded);schema.validate(command,'command')
        components=parameter_components(command)
        numeric=catalog_argument_features([c for c,_ in components])
        # Previously unused A coordinates carry unbounded parameter magnitudes
        # without the legacy slot feature's +/-32 clipping. Names/types remain
        # in the original label fields, exact values in the archived command.
        for i,(component,value) in enumerate(components):
            if value is not None:
                if not math.isfinite(value):raise ValueError('Nonfinite command parameter')
                numeric[i,90]=math.copysign(math.log1p(abs(value)),value)/8
                numeric[i,91]=1
            numeric[i,92]=component.get('Choice')=='null'
            numeric[i,93]=component.get('Choice')=='bool'
            numeric[i,94]=component.get('Choice')=='array'
            numeric[i,95]=component.get('Choice')=='text'
        refs=candidate_entities(observation,command)
        texts=text_assets.encode_bindings(refs)
        vectors=[v for item in texts for v in item['vectors']]
        # Only known printed rules contribute. Structural parameters without a
        # printed identity carry no invented text vector; contextual branch
        # descriptions are supplied separately by the original A residual.
        text=np.zeros((len(components),384),np.float32)
        if vectors:text[-1]=np.stack(vectors).mean(0)
        full=np.concatenate((numeric,text),1)
        history.append(numeric.mean(axis=0))
        if not np.isfinite(full).all():raise ValueError('Nonfinite candidate descriptor')
        start=len(descriptors);descriptors.extend(full)
        sequences.append(list(range(start,start+len(components))))
        weights.append([.5/(len(components)-1)]*(len(components)-1)+[1.])
        operations.append([c for c,_ in components])
        bindings.append([dict(parameter=t['parameter'],identities=t['identities']) for t in texts])
    descriptor=np.asarray(descriptors,np.float32);descriptor.setflags(write=False)
    return dict(commands=tuple(commands),sequences=sequences,descriptors=descriptor,weights=weights,
                history_features=np.asarray(history,np.float32),
                operations=operations,rule_bindings=bindings,
                commands_sha256=hashlib.sha256(canonical(list(commands)).encode()).hexdigest(),
                descriptor_schema='baseline_A_numeric96_rule384_solo_parameters_v1',
                status='candidate_inputs_only_contextual_residual_required')
