"""Staged single-position, 255-slot decision readout. Not deployed."""
import re
import torch
from torch import nn
from semantic_public_text import SemanticPublicText,public_features

def semantic_document(original):
    doc=dict(original)
    doc['state']='公开局面：\n'+SemanticPublicText().summary(public_features(original['state']))
    # Exact IDs remain for identity; source names help read the facts.
    for line in original['rules'].splitlines():
        identity,sep,rule=line.partition('：')
        name=re.search(r'(?:card_name_cn|card_name|monster_name):([^|\n]+)',rule)
        if sep and name:
            doc['state']=re.sub(r'(?<![A-Za-z0-9_])'+re.escape(identity)+r'(?![A-Za-z0-9_])',name.group(1)+'（'+identity+'）',doc['state'])
    assert 'state.' not in doc['state']
    return doc

def encode_final_document(tok,doc,max_tokens=8192):
    k=len(doc['options'])
    if not 1<=k<=255:raise ValueError('Node requires further lossless factoring: options outside 1..255')
    text=doc['state']+'\n原始规则：\n'+doc['rules']+'\n问题：'+doc['question']+'\n合法选项：\n'
    text+='\n'.join(f'选项{i+1:03d}：{option}' for i,option in enumerate(doc['options']))
    text+='\n请对以上编号选项作出决策。'
    ids=[tok.bos_token_id]+tok.encode(text,add_special_tokens=False)+[tok.eos_token_id]
    if len(ids)>max_tokens:raise ValueError(f'Full input {len(ids)} exceeds {max_tokens}; no truncation')
    return ids,len(ids)-1,k

class FinalTokenHeads(nn.Module):
    """Same trunk shape for actor/critic; independent weights, logits vs scalar.

    Structured green features are retained, not replaced by the text summary.
    Critic must use the root pre-action context, never the selected child prefix.
    """
    def __init__(self,structured_width=128):
        super().__init__()
        def trunk():return nn.Sequential(nn.LayerNorm(384+structured_width),nn.Linear(384+structured_width,128),nn.GELU())
        self.actor=nn.Sequential(trunk(),nn.Linear(128,255))
        self.critic=nn.Sequential(trunk(),nn.Linear(128,1))
    def logits(self,last_token,structured,counts):
        if bool(((counts<1)|(counts>255)).any()):raise ValueError('Invalid legal count')
        x=torch.cat((last_token,structured),-1)
        logits=self.actor(x)
        mask=torch.arange(255,device=logits.device)[None,:]<counts[:,None]
        return logits.masked_fill(~mask,-torch.inf)
    def value(self,root_last_token,structured):
        return self.critic(torch.cat((root_last_token,structured),-1)).squeeze(-1)
