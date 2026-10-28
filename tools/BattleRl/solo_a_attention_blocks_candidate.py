"""Batch original 512-query blocks without increasing each block's Q/K span."""
from collections import defaultdict
import torch
import torch.nn.functional as F


def attention(module,q,k,v,attention_mask,dropout=0.,scaling=None,**kwargs):
    if dropout:
        raise ValueError('Frozen inference only')
    lengths=module.config._hierarchical_lengths
    length=q.shape[-2]
    window=module.config.sliding_window if module.sliding_window is not None else None
    groups=defaultdict(list)
    for start in range(0,length,512):
        end=min(length,start+512)
        left,right=(0,length) if window is None else (max(0,start-window),min(length,end+window))
        groups[(end-start,right-left)].append((start,end,left,right))
    parts={}
    for blocks in groups.values():
        for offset in range(0,len(blocks),4):
            selected=blocks[offset:offset+4]
            qs=[];ks=[];vs=[];masks=[]
            for start,end,left,right in selected:
                qs.append(q[:,:,start:end]);ks.append(k[:,:,left:right]);vs.append(v[:,:,left:right])
                keys=torch.arange(left,right,device=q.device)
                mask=keys[None,None,None,:]<lengths[:,None,None,None]
                if window is not None:
                    local=(torch.arange(start,end,device=q.device)[:,None]-keys[None,:]).abs()<=window
                    mask=mask & local[None,None,:,:]
                masks.append(mask)
            output=F.scaled_dot_product_attention(torch.cat(qs),torch.cat(ks),torch.cat(vs),
                attn_mask=torch.cat(masks),dropout_p=0.,scale=scaling)
            for block,result in zip(selected,output.split(q.shape[0])):
                parts[block[0]]=result
    return torch.cat([parts[start] for start in sorted(parts)],dim=2).transpose(1,2),None
