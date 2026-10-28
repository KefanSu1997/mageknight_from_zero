"""Isolated equivalent frozen-attention candidate; never installed by import."""
import torch
import torch.nn.functional as F

def attention(module,q,k,v,attention_mask,dropout=0.,scaling=None,**kwargs):
    if dropout:raise ValueError('Frozen inference only')
    lengths=module.config._hierarchical_lengths
    length=q.shape[-2]
    window=module.config.sliding_window if module.sliding_window is not None else None
    # Global layers need only valid-key padding, without query-sized expansion.
    if window is None:
        mask=torch.arange(length,device=q.device)[None,None,None,:]<lengths[:,None,None,None]
        out=F.scaled_dot_product_attention(q,k,v,attn_mask=mask,dropout_p=0.,scale=scaling)
    else:
        # Same <= window and valid-key rule; cap boolean mask allocation.
        budget=64*2**20
        chunk=max(512,min(length,budget//max(1,len(lengths)*length)))
        parts=[]
        for start in range(0,length,chunk):
            end=min(length,start+chunk);left=max(0,start-window);right=min(length,end+window)
            keys=torch.arange(left,right,device=q.device)
            mask=(keys[None,None,None,:]<lengths[:,None,None,None]) & ((torch.arange(start,end,device=q.device)[:,None]-keys[None,:]).abs()<=window)[None,None,:,:]
            parts.append(F.scaled_dot_product_attention(q[:,:,start:end],k[:,:,left:right],v[:,:,left:right],attn_mask=mask,dropout_p=0.,scale=scaling))
        out=torch.cat(parts,dim=2)
    return out.transpose(1,2),None
