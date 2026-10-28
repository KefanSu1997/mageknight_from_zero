"""Candidate-only batching experiment. No pruning, parameter or feature change.

Each tree retains its own root/parent graph. The unused top-down state context
is omitted; only root encodings are consumed by the existing candidate head.
Not enabled in the running policy until forward/gradient/CUDA gates pass.
"""
import numpy as np
import torch
from torch.nn.utils.rnn import pack_sequence


def roots(encoder, candidates, node_budget=4096):
    if not candidates:raise ValueError('Nonempty support required')
    chunks=[];current=[];size=0
    for row in candidates:
        n=len(row['numeric']['nodes'])
        if current and size+n>node_budget:
            chunks.append(current);current=[];size=0
        current.append(row);size+=n
    if current:chunks.append(current)
    return torch.cat([_chunk(encoder,chunk) for chunk in chunks])


def _chunk(e, candidates, with_context=False):
    rows=[];ints=[];nums=[];masks=[];texts=[];roots=[];depths=[]
    for candidate in candidates:
        p=candidate['numeric'];mask=candidate['node_mask']
        if p['schema_sha256']!=e.layout.schema.signature:raise ValueError('Schema mismatch')
        offset=len(rows);text_offset=sum(len(t) for t in texts);roots.append(offset)
        nodes=p['nodes'].copy();local_depth=[0]*len(nodes)
        for i,row in enumerate(nodes):
            if i:
                parent=int(row[0])
                if not 0<=parent<i:raise ValueError('Broken tree')
                local_depth[i]=local_depth[parent]+1;row[0]+=offset
            row[5]+=text_offset
        rows.extend(nodes);depths.extend(local_depth);ints.extend(p['integers']);nums.extend(p['numbers']);masks.extend(mask);texts.append(p['text'])
    rows=np.asarray(rows);raw=np.concatenate(texts);device=e.types.weight.device;n=len(rows)
    nodes=torch.as_tensor(rows,device=device,dtype=torch.long)
    keep=torch.as_tensor(masks,device=device,dtype=torch.bool)
    values=torch.as_tensor(ints,device=device,dtype=torch.float32);floats=torch.as_tensor(nums,device=device,dtype=torch.float32)
    slog=lambda x:torch.sign(x)*torch.log1p(torch.abs(x))
    numeric=torch.stack((slog(values),slog(floats),slog(nodes[:,2].float()),torch.log1p(nodes[:,6].float())),1)
    strings=[];dest=[]
    for i,row in enumerate(rows):
        if row[4]==3 and row[6]>0 and masks[i]:
            strings.append(e.bytes(torch.as_tensor(raw[row[5]:row[5]+row[6]].copy(),device=device,dtype=torch.long)));dest.append(i)
    text=e.types.weight.new_zeros(n,32)
    if strings:
        _,hidden=e.string(pack_sequence(strings,enforce_sorted=False))
        text=text.index_copy(0,torch.tensor(dest,device=device),hidden[0])
    base=e.leaf(torch.cat((e.types(nodes[:,3]),e.fields(nodes[:,1]+1),e.kinds(nodes[:,4]),text,numeric),1))*keep[:,None]
    encoded=base;depth=torch.tensor(depths,device=device)
    for level in range(max(depths),0,-1):
        child=torch.nonzero((depth==level)&keep,as_tuple=False).flatten()
        if len(child)==0:continue
        parents=nodes[child,0]
        sums=base.new_zeros(n,e.width).index_add(0,parents,encoded[child])
        counts=base.new_zeros(n).index_add(0,parents,base.new_ones(len(child)))
        touched=torch.unique(parents)
        update=e.child_fusion(torch.cat((base[touched],sums[touched]/counts[touched,None],torch.log1p(counts[touched,None])),1))
        encoded=encoded.index_copy(0,touched,update*keep[touched,None])
    if not with_context:
        return encoded[torch.tensor(roots,device=device)]
    context=encoded
    for level in range(1,max(depths)+1):
        child=torch.nonzero((depth==level)&keep,as_tuple=False).flatten()
        update=e.context(torch.cat((encoded[child],context[nodes[child,0]]),1))
        context=context.index_copy(0,child,update)
    if not torch.isfinite(context).all():raise ValueError('Nonfinite structured encoding')
    output=[]
    for candidate,offset in zip(candidates,roots):
        local=candidate['numeric']['nodes'];paths=[()]*len(local)
        for i,(parent,field,index,*_) in enumerate(local):
            if i:paths[i]=paths[parent]+((e.layout.fields[field] if field>=0 else int(index)),)
        output.append((encoded[offset],context[offset:offset+len(local)],{path:i for i,path in enumerate(paths)}))
    return output


def state_contexts(encoder, prepared_states, node_budget=8192):
    """Independent full state trees, including top-down entity attributes."""
    output=[];chunk=[];size=0
    for prepared in prepared_states:
        row=dict(numeric=prepared['state'],node_mask=prepared['state_node_mask'])
        count=len(row['numeric']['nodes'])
        if chunk and size+count>node_budget:
            output.extend(_chunk(encoder,chunk,with_context=True));chunk=[];size=0
        chunk.append(row);size+=count
    if chunk:output.extend(_chunk(encoder,chunk,with_context=True))
    return output
