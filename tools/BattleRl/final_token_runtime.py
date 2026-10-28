"""Semantic full-rules nodes, one final token, batched immutable frozen cache."""
import json, os, re, time
from pathlib import Path
from collections import OrderedDict
import numpy as np
import torch
from hierarchical_batch_encoder import BatchedReader
from final_token_option import encode_final_document, semantic_document
from hierarchical_packed_cache import PackedVectors
from joint_option_policy import ContextStore
import hierarchical_option_policy as h

LABELS={'Kind':'操作类型','Card':'牌或技能','Choice':'发动模式','完成候选':'完整候选编号',
        'play':'出牌','attack':'攻击','block':'格挡','damage':'承伤','end':'结束',
        'base':'基础效果','strong':'强化效果','advance':'进入下一战斗阶段','sideways':'横置使用'}

def edge_text(edge, rules):
    key,value=edge
    for old,new in [('完整操作','完整操作'),('/Kind','/操作类型'),('/Card','/牌或技能'),('/Choice','/发动模式')]:
        key=key.replace(old,new)
    result=key+'='+LABELS.get(value,value)
    for identity,text in rules.items():
        name=re.search(r'(?:card_name_cn|card_name|monster_name):([^|\n]+)',text)
        if name:result=re.sub(r'(?<![A-Za-z0-9_])'+re.escape(identity)+r'(?![A-Za-z0-9_])',name.group(1)+'（'+identity+'）',result)
    return result

def node_document(tree,base,node):
    rules=dict(line.split('：',1) for line in base['rules'].splitlines() if '：' in line)
    current=tree.nodes[node]
    prefix='；'.join(edge_text(e,rules) for e in current['prefix']) or '尚未选择'
    options=[edge_text(e,rules) for e in current['children']]
    if not options:options=['唯一完整操作，直接执行']
    return dict(state=base['state']+'\n本次完整操作已锁定（不可取消或回退）：'+prefix,
                rules=base['rules'],question='选择当前层的一项合法后继；全部参数确定后才执行完整操作。',options=options)

class FinalReader(BatchedReader):
    def encode_many(self,documents,batch_size=None):
        batch_size=batch_size or int(os.environ.get('BATTLE_RL_ENCODER_BATCH','8'))
        encoded=[encode_final_document(self.tok,d,8192) for d in documents]
        order=sorted(range(len(encoded)),key=lambda i:len(encoded[i][0]));result=[None]*len(encoded)
        self.model.config._attn_implementation='hierarchical_batch'
        for layer in self.model.layers:layer.mlp.forward=layer.mlp._joint_original_forward
        with torch.inference_mode():
            offset=0
            while offset<len(order):
                size=min(batch_size,len(order)-offset)
                while size>1 and size*len(encoded[order[offset+size-1]][0])>16000:size-=1
                indices=order[offset:offset+size];offset+=size
                lengths=[len(encoded[i][0]) for i in indices]
                x=torch.full((size,max(lengths)),self.tok.pad_token_id,device='cuda',dtype=torch.long)
                for j,i in enumerate(indices):x[j,:lengths[j]]=torch.as_tensor(encoded[i][0],device='cuda')
                self.model.config._hierarchical_lengths=torch.tensor(lengths,device='cuda')
                z=self.model(input_ids=x,attention_mask={'full_attention':None,'sliding_attention':None}).last_hidden_state
                vectors=z[torch.arange(size,device='cuda'),torch.tensor(lengths,device='cuda')-1].float().cpu().numpy()
                for j,i in enumerate(indices):result[i]=vectors[j].copy()
        if not all(np.isfinite(v).all() for v in result):raise ValueError('Nonfinite final vectors')
        self.last_lengths=[len(x[0]) for x in encoded]
        return result

class Runtime:
    def __init__(self,path):
        self.path=str(path);self.cache=PackedVectors(Path(path)/'final_vectors_v1');self.memory=OrderedDict()
        self.trees=OrderedDict();self.reader=None
    def tree(self,context):
        if context not in self.trees:
            doc=h.store(self.path).read(context)
            self.trees[context]=(h.ChoiceTree(doc['operations']),doc)
            if len(self.trees)>256:self.trees.popitem(last=False)
        self.trees.move_to_end(context);return self.trees[context]
    def root(self,context):return self.tree(context)[0].branch(0) if context else 0
    def vectors(self,requests):
        requests=[(int(c),int(n)) for c,n in requests];pending=[];docs=[]
        for key in dict.fromkeys(requests):
            if not key[0] or key in self.memory:continue
            v=self.cache.get(key)
            if v is None:
                t,base=self.tree(key[0]);pending.append(key);docs.append(node_document(t,base,key[1]))
            else:self.memory[key]=v
        if docs:
            if self.reader is None:self.reader=FinalReader()
            started=time.perf_counter();vectors=self.reader.encode_many(docs)
            self.cache.put_many(zip(pending,vectors));self.memory.update(zip(pending,vectors))
            with (Path(self.path)/'encoder_timing.jsonl').open('a') as f:
                f.write(json.dumps(dict(nodes=len(docs),tokens=sum(self.reader.last_lengths),max_tokens=max(self.reader.last_lengths),seconds=time.perf_counter()-started))+'\n')
        result=np.stack([self.memory[k] if k[0] else np.zeros(384,np.float32) for k in requests])
        for k in requests:
            if k[0]:self.memory.move_to_end(k)
        while len(self.memory)>16384:self.memory.popitem(last=False)
        return result

_runtimes={}
def runtime(path):
    path=str(Path(path).resolve())
    if path not in _runtimes:_runtimes[path]=Runtime(path)
    return _runtimes[path]

def install_journal():
    # Only the append-only journal optimization; never install old marker policy.
    from hierarchical_packed_cache import add,read
    ContextStore.add=add;ContextStore.read=read
