"""Explicit public-observation/catalog expansion; never reuse positional heads."""
import numpy as np


def expand_arrays(values, old_actions, new_actions, old_observations, new_observations):
    if new_observations < old_observations:
        raise ValueError('Only declared appended public observations supported')
    old_keys=[a['Key'] for a in old_actions];new_keys=[a['Key'] for a in new_actions]
    if len(set(old_keys))!=len(old_keys) or len(set(new_keys))!=len(new_keys):
        raise ValueError('Duplicate complete action keys')
    by_key={a['Key']:(i,a) for i,a in enumerate(new_actions)}
    if any(k not in by_key or by_key[k][1]!=a for k,a in zip(old_keys,old_actions)):
        raise ValueError('Existing original action definition removed or edited')
    positions=np.array([by_key[k][0] for k in old_keys])
    old_kinds=sorted({a['Kind'] for a in old_actions});new_kinds=sorted({a['Kind'] for a in new_actions})
    kinds=np.array([new_kinds.index(k) for k in old_kinds])
    result={k:v.copy() for k,v in values.items()}
    # Previous action embedding and raw reward follow the observation vector;
    # they must move past the appended public rows, rather than prefix-copying.
    first=values['core.encoder_w0'];tail=first.shape[0]-old_observations
    if tail!=33:raise ValueError('Expected32 action embedding plus1 raw reward row')
    expanded=np.zeros((new_observations+tail,first.shape[1]),dtype=first.dtype)
    expanded[:old_observations]=first[:old_observations]
    expanded[new_observations:]=first[old_observations:]
    result['core.encoder_w0']=expanded
    for key,axis,size,mapping in [('head.wp',1,len(new_actions),positions),
                                  ('head.bp',0,len(new_actions),positions),
                                  ('previous_action_embedding',0,len(new_actions),positions),
                                  ('head.wk',1,len(new_kinds),kinds),('head.bk',0,len(new_kinds),kinds)]:
        old=values[key];shape=list(old.shape);shape[axis]=size
        new=np.zeros(shape,dtype=old.dtype)
        if axis==0:new[mapping]=old
        else:new[:,mapping]=old
        result[key]=new
    return result
