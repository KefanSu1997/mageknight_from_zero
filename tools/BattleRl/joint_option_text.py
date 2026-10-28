"""Public state and ALL complete options in one contextual text sequence.

No hidden state dictionary is accepted. No token truncation or candidate pruning.
The frozen encoder reads the full joint sequence, not independent card embeddings.
"""
import json
import re
import sys
from pathlib import Path
import numpy as np
from public_entity_tokens import PublicEntityTokens, original_vocabulary

ROOT=Path(__file__).resolve().parents[2]
SNAPSHOT=Path.home()/'.cache/huggingface/hub/models--jhu-clsp--mmBERT-small/snapshots/abc32620dd4f6ab06f5fbe905dc25f310618e09f'

def tokenizer():
    # Reuse installed tokenizer dependencies; never replace live CUDA torch.
    dependencies=str(ROOT/'.venv_ui_text/Lib/site-packages')
    if dependencies not in sys.path:sys.path.append(dependencies)
    from transformers import AutoTokenizer
    return AutoTokenizer.from_pretrained(SNAPSHOT,local_files_only=True,trust_remote_code=False)

class JointRenderer:
    def __init__(self,feature_names,text_assets):
        self.names=feature_names
        assert len(self.names)==6314
        self.layout=PublicEntityTokens(original_vocabulary())
        self.rules=json.loads((Path(text_assets)/'texts.json').read_text(encoding='utf-8'))['entities']

    def primitive(self,action):
        labels={'Kind':'操作','Card':'牌或技能','Choice':'发动模式','Input':'完整参数'}
        # Preserve every real field. Key is an internal identity, not extra rules.
        return '，'.join(labels.get(k,k)+'='+str(v) for k,v in action.items() if k!='Key' and v not in ('',None))

    def render(self,features,previous,choices):
        features=np.asarray(features,dtype=np.float32)
        if features.shape!=(6314,) or not np.isfinite(features).all():raise ValueError('Public feature contract')
        _,visible=self.layout._one(features)
        identities={i for i,_,_ in visible if i!='_public_global_'}
        options=['；随后'.join(self.primitive(a) for a in seq)+'。完成以上全部步骤。' for seq in choices]
        if not options:options=['终局占位（不执行）']
        combined='\n'.join(options)
        # Add each referenced printed rule once; options share this glossary.
        for identity in self.rules:
            if identity!='_public_global_' and identity in combined:identities.add(identity)
        facts='；'.join(str(self.names[i])+'='+format(float(v),'.9g') for i,v in enumerate(features) if v!=0)
        history=','.join(format(float(v),'.9g') for v in np.asarray(previous,dtype=np.float32))
        state='公开状态（未列坐标值为0；数值沿用原观察归一化单位）：'+facts+'\n上一步完整操作特征及奖励：'+history
        glossary='\n'.join(i+'：'+self.rules[i] for i in sorted(identities))
        question='根据当前公开战斗状态和原卡规则，选择一项完整合法操作以赢得本场战斗。所有候选都合法，必须选择其中一项。'
        return dict(state=state,rules=glossary,question=question,options=options)

def encode_document(tok,document,max_tokens=8192):
    # Explicit marker positions, avoiding accidental marker strings in rule text.
    prefix=document['state']+'\n原始规则：\n'+document['rules']+'\n问题：'+document['question']+'\n完整候选：\n'
    ids=[tok.bos_token_id]+tok.encode(prefix,add_special_tokens=False)
    positions=[]
    for option in document['options']:
        positions.append(len(ids));ids.append(tok.mask_token_id)
        ids.extend(tok.encode(option+'\n',add_special_tokens=False))
    ids.append(tok.eos_token_id)
    if len(ids)>8192 and max_tokens is not None:
        # Lossless shared-step dictionary. Every option still has its own marker,
        # exact ordered full operation, and access to all expanded definitions.
        suffix='。完成以上全部步骤。'
        primitives=[];lookup={};references=[]
        for option in document['options']:
            body=option[:-len(suffix)] if option.endswith(suffix) else option
            parts=body.split('；随后');refs=[]
            for part in parts:
                if part not in lookup:lookup[part]=len(primitives);primitives.append(part)
                refs.append(lookup[part])
            references.append(refs)
        definition='\n共享步骤定义（选项中的编号按顺序全部执行，编号只引用完整参数，不省略步骤）：\n'+''.join(f'{i}={s}\n' for i,s in enumerate(primitives))
        ids=[tok.bos_token_id]+tok.encode(prefix+definition,add_special_tokens=False);positions=[]
        for refs in references:
            positions.append(len(ids));ids.append(tok.mask_token_id)
            ids.extend(tok.encode(','.join(map(str,refs))+'\n',add_special_tokens=False))
        ids.append(tok.eos_token_id)
        # Decode back to the exact original option text before accepting factoring.
        for option,refs in zip(document['options'],references):
            rebuilt='；随后'.join(primitives[i] for i in refs)+(suffix if option.endswith(suffix) else '')
            assert rebuilt==option
    if max_tokens is not None and len(ids)>max_tokens:
        raise ValueError(f'Joint context has {len(ids)} tokens > {max_tokens}; all {len(positions)} options preserved, refusing truncation')
    return ids,positions
