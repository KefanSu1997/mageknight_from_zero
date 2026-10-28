"""Padded batch encoding with exact valid-key and pretrained local-window masks."""
import os
import numpy as np
import torch
import torch.nn.functional as F
from joint_option_policy import ContextReader
from joint_option_text import encode_document

def attention(module,q,k,v,attention_mask,dropout=0.,scaling=None,**kwargs):
    if dropout:raise ValueError('Frozen inference only')
    lengths=module.config._hierarchical_lengths
    length=q.shape[-2];parts=[]
    window=module.config.sliding_window if module.sliding_window is not None else None
    for start in range(0,length,512):
        end=min(length,start+512)
        left,right=(0,length) if window is None else (max(0,start-window),min(length,end+window))
        keys=torch.arange(left,right,device=q.device)
        mask=keys[None,None,None,:]<lengths[:,None,None,None]
        if window is not None:
            local=(torch.arange(start,end,device=q.device)[:,None]-keys[None,:]).abs()<=window
            mask=mask & local[None,None,:,:]
        parts.append(F.scaled_dot_product_attention(q[:,:,start:end],k[:,:,left:right],v[:,:,left:right],attn_mask=mask,dropout_p=0.,scale=scaling))
    return torch.cat(parts,dim=2).transpose(1,2),None

class BatchedReader(ContextReader):
    def __init__(self):
        super().__init__();self.max_tokens=8192
        from transformers.modeling_utils import ALL_ATTENTION_FUNCTIONS
        ALL_ATTENTION_FUNCTIONS.register('hierarchical_batch',attention)
    def encode(self,document):
        return self.encode_many([document])[0]
    def encode_many(self,documents,batch_size=None):
        batch_size=batch_size or int(os.environ.get('BATTLE_RL_ENCODER_BATCH','4'))
        encoded=[encode_document(self.tok,d,self.max_tokens) for d in documents]
        result=[None]*len(encoded)
        order=sorted(range(len(encoded)),key=lambda i:len(encoded[i][0]))
        self.model.config._attn_implementation='hierarchical_batch'
        for layer in self.model.layers:
            layer.mlp.forward=layer.mlp._joint_original_forward
        with torch.inference_mode():
            for offset in range(0,len(order),batch_size):
                indices=order[offset:offset+batch_size];lengths=[len(encoded[i][0]) for i in indices]
                x=torch.full((len(indices),max(lengths)),self.tok.pad_token_id,device='cuda',dtype=torch.long)
                for j,i in enumerate(indices):x[j,:lengths[j]]=torch.tensor(encoded[i][0],device='cuda')
                self.model.config._hierarchical_lengths=torch.tensor(lengths,device='cuda')
                hidden=self.model(input_ids=x,attention_mask={'full_attention':None,'sliding_attention':None}).last_hidden_state
                for j,i in enumerate(indices):result[i]=hidden[j,[0]+encoded[i][1]].float().cpu().numpy()
        if not all(np.isfinite(v).all() for v in result):raise ValueError('Nonfinite batched encoding')
        return result
