"""Exact attention query tiling, not context/option chunking.

Global layers still attend to every key in the same sequence. Local layers keep
the pretrained inclusive window. Single unpadded frozen-encoder inputs only.
"""
import torch
import torch.nn.functional as F
import types

def tiled_attention(module,query,key,value,attention_mask,dropout=0.,scaling=None,**kwargs):
    if dropout or attention_mask is not None or query.shape[0]!=1:raise ValueError('Frozen unpadded singleton attention only')
    length=query.shape[-2];outputs=[]
    window=module.config.sliding_window if module.sliding_window is not None else None
    for start in range(0,length,256):
        end=min(length,start+256)
        left,right=(0,length) if window is None else (max(0,start-window),min(length,end+window))
        mask=None
        if window is not None:
            qpos=torch.arange(start,end,device=query.device)[:,None]
            kpos=torch.arange(left,right,device=query.device)[None,:]
            mask=(qpos-kpos).abs()<=window
        outputs.append(F.scaled_dot_product_attention(query[:,:,start:end],key[:,:,left:right],value[:,:,left:right],
            attn_mask=mask,dropout_p=0.,is_causal=False,scale=scaling))
    return torch.cat(outputs,dim=2).transpose(1,2),None

def enable(model):
    from transformers.modeling_utils import ALL_ATTENTION_FUNCTIONS
    ALL_ATTENTION_FUNCTIONS.register('joint_query_tiled',tiled_attention)
    model.config._attn_implementation='joint_query_tiled'
    for layer in model.layers:
        mlp=layer.mlp
        if hasattr(mlp,'_joint_original_forward'):continue
        mlp._joint_original_forward=mlp.forward
        def chunked_mlp(self,x):
            return torch.cat([self._joint_original_forward(p) for p in x.split(256,dim=1)],dim=1)
        mlp.forward=types.MethodType(chunked_mlp,mlp)
