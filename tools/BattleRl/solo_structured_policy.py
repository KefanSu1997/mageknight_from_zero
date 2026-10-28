"""Full public DTO front end for the fresh A-family recurrent policy.

Schema-defined fields/types, dynamic arrays, and all UTF-8 bytes are encoded.
No fixed map/hand/action cap and no learned vocabulary derived from validation.
The neural compression is learned; the archived input remains lossless.
"""
import numpy as np
import torch
from torch import nn
from torch.nn.utils.rnn import pack_sequence
from solo_a_policy import SoloAPolicy
from solo_public_tensors import PublicTensorizer


class PublicTreeEncoder(nn.Module):
    def __init__(self, schema, width=128):
        super().__init__()
        self.layout=PublicTensorizer(schema)
        self.width=width
        self.types=nn.Embedding(len(self.layout.types),32)
        self.fields=nn.Embedding(len(self.layout.fields)+1,32)
        self.kinds=nn.Embedding(8,16)
        self.bytes=nn.Embedding(256,16)
        self.string=nn.GRU(16,32,batch_first=True)
        self.leaf=nn.Sequential(nn.Linear(32+32+16+32+4,width),nn.Tanh())
        self.child_fusion=nn.Sequential(nn.Linear(width*2+1,width),nn.Tanh())
        self.context=nn.Sequential(nn.Linear(width*2,width),nn.Tanh())

    def forward(self, packed, mask):
        if packed['schema_sha256']!=self.layout.schema.signature:
            raise ValueError('Neural input schema differs')
        device=self.types.weight.device
        rows=packed['nodes']; n=len(rows)
        nodes=torch.as_tensor(rows,device=device,dtype=torch.long)
        keep=torch.as_tensor(mask,device=device,dtype=torch.bool)
        depth=[0]*n; paths=[()]*n
        for i,(parent,field,index,*_) in enumerate(rows):
            if i:
                if not 0 <= parent < i:raise ValueError('Broken tree parent')
                depth[i]=depth[parent]+1
                paths[i]=paths[parent]+((self.layout.fields[field] if field>=0 else int(index)),)
        values=torch.as_tensor(packed['integers'],device=device,dtype=torch.float32)
        floats=torch.as_tensor(packed['numbers'],device=device,dtype=torch.float32)
        indices=nodes[:,2].float()
        slog=lambda x:torch.sign(x)*torch.log1p(torch.abs(x))
        numeric=torch.stack((slog(values),slog(floats),slog(indices),torch.log1p(nodes[:,6].float())),1)
        strings=[]; destinations=[]
        for i,row in enumerate(rows):
            if row[4]==3 and row[6]>0 and mask[i]:
                raw=packed['text'][row[5]:row[5]+row[6]].copy()
                strings.append(self.bytes(torch.as_tensor(raw,device=device,dtype=torch.long)))
                destinations.append(i)
        text=self.types.weight.new_zeros(n,32)
        if strings:
            _, hidden=self.string(pack_sequence(strings,enforce_sorted=False))
            text=text.index_copy(0,torch.tensor(destinations,device=device),hidden[0])
        base=self.leaf(torch.cat((self.types(nodes[:,3]),self.fields(nodes[:,1]+1),
                                 self.kinds(nodes[:,4]),text,numeric),1))*keep[:,None]
        encoded=base
        depths=torch.tensor(depth,device=device)
        for level in range(max(depth),0,-1):
            child=torch.nonzero((depths==level)&keep,as_tuple=False).flatten()
            if len(child)==0:continue
            parents=nodes[child,0]
            sums=base.new_zeros(n,self.width).index_add(0,parents,encoded[child])
            counts=base.new_zeros(n).index_add(0,parents,base.new_ones(len(child)))
            touched=torch.unique(parents)
            update=self.child_fusion(torch.cat((base[touched],sums[touched]/counts[touched,None],
                                            torch.log1p(counts[touched,None])),1))
            encoded=encoded.index_copy(0,touched,update*keep[touched,None])
        context=encoded
        for level in range(1,max(depth)+1):
            child=torch.nonzero((depths==level)&keep,as_tuple=False).flatten()
            update=self.context(torch.cat((encoded[child],context[nodes[child,0]]),1))
            context=context.index_copy(0,child,update)
        if not torch.isfinite(context).all():raise ValueError('Nonfinite structured encoding')
        return encoded[0],context,{path:i for i,path in enumerate(paths)}


class SoloStructuredPolicy(nn.Module):
    def __init__(self, schema, catalog_signature):
        super().__init__()
        self.schema_signature=schema.signature
        self.catalog_signature=catalog_signature
        self.structured=PublicTreeEncoder(schema)
        self.policy=SoloAPolicy(128,128,128)

    def get_extra_state(self):
        return dict(schema=self.schema_signature,catalog=self.catalog_signature)

    def set_extra_state(self,state):
        if state!=self.get_extra_state():raise ValueError('Checkpoint input binding differs')

    def evaluate_sequence(self, prepared_steps, actions, memory=None, episode_starts=None):
        """Differentiable recurrent likelihood replay without hidden-state detach.

        Supports change at every decision. There is no padded global action
        dimension or cancellation of a previously committed native operation.
        Minibatch adapters must provide the actual sequence-start memory.
        """
        actions=list(actions)
        if not actions:raise ValueError('Empty sequence')
        starts=[False]*len(actions) if episode_starts is None else list(episode_starts)
        if len(starts)!=len(actions):raise ValueError('Sequence boundary length differs')
        values=[];logs=[];entropies=[]
        iterator=iter(prepared_steps)
        for index,(action,start) in enumerate(zip(actions,starts)):
            try:prepared=next(iterator)
            except StopIteration:raise ValueError('Missing sequence observation') from None
            if isinstance(action,(bool,np.bool_)) or not isinstance(action,(int,np.integer)):
                raise ValueError('Action must be an integer support index')
            if not 0<=action<len(prepared['candidates']):raise ValueError('Action outside archived support')
            logits,value,memory=self.step(prepared,memory,episode_start=bool(start))
            distribution=torch.distributions.Categorical(logits=logits)
            values.append(value)
            logs.append(distribution.log_prob(torch.tensor(int(action),device=logits.device)))
            entropies.append(distribution.entropy())
        if next(iterator,None) is not None:raise ValueError('Extra sequence observation')
        return torch.stack(values),torch.stack(logs),torch.stack(entropies),memory

    def step(self, prepared, memory=None, episode_start=False):
        if prepared['schema_sha256']!=self.schema_signature or prepared['catalog_signature']!=self.catalog_signature:
            raise ValueError('Prepared input version differs')
        device=next(self.parameters()).device
        tensor=lambda x:torch.as_tensor(np.array(x,copy=True),device=device,dtype=torch.float32)
        state,attributes,paths=self.structured(prepared['state'],prepared['state_node_mask'])
        texts=[]; attrs=[]
        for ref, encoded in zip(prepared['state_references'],prepared['state_texts']):
            path=tuple(ref['zone'])+(() if ref['slot'] is None else (ref['slot'],))
            index=paths[path]
            for vector in encoded['vectors']:
                texts.append(tensor(vector));attrs.append(attributes[index])
        text=torch.stack(texts) if texts else state.new_empty(0,384)
        attr=torch.stack(attrs) if attrs else state.new_empty(0,128)
        candidates=self.candidate_roots(prepared['candidates']); candidate_text=[]
        for row in prepared['candidates']:
            vectors=[tensor(v) for ref in row['texts'] for v in ref['vectors']]
            candidate_text.append(torch.stack(vectors) if vectors else state.new_empty(0,384))
        return self.policy.step(state,text,attr,candidates,candidate_text,memory,episode_start)

    def candidate_roots(self,candidates):
        if not candidates:raise ValueError('Terminal observations cannot be sampled')
        return torch.stack([self.structured(r['numeric'],r['node_mask'])[0] for r in candidates])
