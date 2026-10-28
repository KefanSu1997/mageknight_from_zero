"""Canonical key order matches persisted SAB1 trees before cache insertion."""
from solo_a_decoded_cache_v2_candidate import DecodedCacheRuntimeV2

def canonical(value):
    if isinstance(value,dict):
        return {k:canonical(value[k]) for k in sorted(value)}
    if isinstance(value,list):return [canonical(x) for x in value]
    if isinstance(value,tuple):return tuple(canonical(x) for x in value)
    return value

class DecodedCacheRuntimeV3(DecodedCacheRuntimeV2):
    def add(self,support,document):
        # ChoiceTree visits action.items() in insertion order. JSON uses
        # sort_keys=True; in-memory paths must use that same order from outset.
        return super().add(canonical(support),canonical(document))
