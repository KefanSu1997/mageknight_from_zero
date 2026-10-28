"""Isolated transfer optimization; not deployed until numerical gates pass."""
import numpy as np
import torch
from solo_forest_policy import SoloForestPolicy

class SoloBulkTextPolicy(SoloForestPolicy):
    def step(self,prepared,memory=None,episode_start=False):
        if prepared['schema_sha256']!=self.schema_signature or prepared['catalog_signature']!=self.catalog_signature:
            raise ValueError('Prepared input version differs')
        device=next(self.parameters()).device
        state,attributes,paths=self.state_encoding(prepared)
        vectors=[];indices=[]
        for ref,encoded in zip(prepared['state_references'],prepared['state_texts']):
            path=tuple(ref['zone'])+(() if ref['slot'] is None else (ref['slot'],))
            for vector in encoded['vectors']:
                vectors.append(vector);indices.append(paths[path])
        state_count=len(vectors);lengths=[]
        for row in prepared['candidates']:
            start=len(vectors)
            vectors.extend(v for ref in row['texts'] for v in ref['vectors'])
            lengths.append(len(vectors)-start)
        all_text=(torch.as_tensor(np.stack(vectors),device=device,dtype=torch.float32)
                  if vectors else state.new_empty(0,384))
        text=all_text[:state_count]
        attr=attributes[torch.tensor(indices,device=device,dtype=torch.long)] if indices else state.new_empty(0,128)
        candidate_text=list(all_text[state_count:].split(lengths))
        candidates=self.candidate_roots(prepared['candidates'])
        return self.core_step(state,text,attr,candidates,candidate_text,memory,episode_start)

    def core_step(self,*args):
        return self.policy.step(*args)

    def state_encoding(self,prepared):
        return self.structured(prepared['state'],prepared['state_node_mask'])
