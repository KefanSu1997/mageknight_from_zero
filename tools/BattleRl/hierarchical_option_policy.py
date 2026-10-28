"""Irreversible internal choices; exactly one formal atomic operation per env.step.

PPO receives log P(complete operation)=sum of conditional branch log probabilities.
No selection/cancel actions or extra rewards are exposed to the environment.
"""
import json,hashlib
from pathlib import Path
from collections import OrderedDict
import numpy as np
import torch
from torch import nn
from joint_option_policy import BattleEnv,JointOptionPolicy,JointFeatures,store,ContextReader

def operation_path(sequence,index):
    end=sequence[-1];path=[]
    for k in ('Kind','Card','Choice'):
        if end.get(k) not in ('',None):path.append(('完整操作/'+k,str(end[k])))
    for step,action in enumerate(sequence):
        for k,v in action.items():
            if k=='Key' or v in ('',None):continue
            if k=='Input':
                for number,part in enumerate(str(v).split(';')):
                    if '=' in part:key,value=part.split('=',1)
                    else:key,value='原参数',part
                    path.append((f'步骤{step+1}/参数{number+1}/{key}',value))
            else:path.append((f'步骤{step+1}/{k}',str(v)))
    # Keeps semantically duplicate candidates distinct without dropping support.
    path.append(('完成候选',str(index)))
    return path

class ChoiceTree:
    def __init__(self,operations):
        self.nodes=[dict(children={},leaf=None,prefix=[])];self.paths=[]
        for leaf,sequence in enumerate(operations):
            node=0;route=[]
            for edge in operation_path(sequence,leaf):
                children=self.nodes[node]['children']
                if edge not in children:
                    children[edge]=len(self.nodes)
                    self.nodes.append(dict(children={},leaf=None,prefix=self.nodes[node]['prefix']+[edge]))
                next_node=children[edge];route.append((node,edge));node=next_node
            assert self.nodes[node]['leaf'] is None
            self.nodes[node]['leaf']=leaf;self.paths.append(route)
        self.leaves=len(operations)
        assert sorted(n['leaf'] for n in self.nodes if n['leaf'] is not None)==list(range(self.leaves))
    def branch(self,node):
        while len(self.nodes[node]['children'])==1:node=next(iter(self.nodes[node]['children'].values()))
        return node
    def document(self,base,node):
        current=self.nodes[node];locked='；'.join(k+'='+v for k,v in current['prefix']) or '尚未选择'
        return dict(state=base['state']+'\n本次操作已锁定（不可取消、修改或回退）：'+locked,rules=base['rules'],
            question='继续确定这次完整操作的一项参数。只能从下面的合法后继中选一项；选齐后才执行规则操作。',
            options=[k+'='+v for k,v in current['children']])

_trees=OrderedDict();_vectors=OrderedDict();_reader=None
def tree(path,context):
    key=(path,context)
    if key not in _trees:
        doc=store(path).read(context);_trees[key]=(ChoiceTree(doc['operations']),doc)
        while len(_trees)>8:_trees.popitem(last=False)
    _trees.move_to_end(key);return _trees[key]
def node_vectors(path,context,node):
    global _reader
    if context==0:return np.zeros((2,384),np.float32)
    key=(path,context,node)
    if key not in _vectors:
        t,doc=tree(path,context);folder=Path(path)/'hierarchical';folder.mkdir(exist_ok=True)
        file=folder/f'{context:08d}_{node:06d}.npy'
        if file.exists():v=np.load(file,allow_pickle=False)
        else:
            if _reader is None:_reader=ContextReader();_reader.max_tokens=8192
            d=t.document(doc,node)
            if not d['options']:d['options']=['唯一完整操作，直接执行']
            v=_reader.encode(d);np.save(file,v,allow_pickle=False)
        _vectors[key]=v
        while len(_vectors)>512:_vectors.popitem(last=False)
    _vectors.move_to_end(key);return _vectors[key]

class HierarchicalFeatures(JointFeatures):
    def forward(self,obs):
        ids=obs['context_id'].detach().cpu().numpy().reshape(-1).astype('int64')
        values=[]
        for i in ids:
            node=tree(self.context_path,int(i))[0].branch(0) if i else 0
            values.append(node_vectors(self.context_path,int(i),node)[0])
        return self.projection(torch.as_tensor(np.stack(values),device=obs['context_id'].device))

class HierarchicalDistribution:
    def __init__(self,policy,latent,contexts):self.policy=policy;self.latent=latent;self.contexts=contexts;self.memo={}
    def categorical(self,row,node):
        key=(row,node)
        if key not in self.memo:
            v=node_vectors(self.policy.context_path,int(self.contexts[row]),node)[1:]
            option=self.policy.option_head(torch.as_tensor(v,device=self.latent.device))
            query=self.policy.action_net(self.latent[row:row+1])
            logits=(query*option).sum(-1)/(128**.5)+self.policy.option_bias(option).squeeze(-1)
            self.memo[key]=torch.distributions.Categorical(logits=logits)
        return self.memo[key]
    def sample_rows(self,generators=None,deterministic=False):
        selected=[];records=[]
        for row,context in enumerate(self.contexts):
            if not context:selected.append(0);continue
            t,_=tree(self.policy.context_path,int(context));node=t.branch(0);route=[]
            while t.nodes[node]['leaf'] is None:
                dist=self.categorical(row,node)
                choice=int(dist.logits.argmax()) if deterministic else int(torch.multinomial(dist.probs,1,generator=None if generators is None else generators[row]))
                children=list(t.nodes[node]['children'].items());edge,next_node=children[choice]
                route.append(dict(node=node,choice=choice,branch_count=len(children),locked=edge))
                assert next_node>node
                node=t.branch(next_node)
            leaf=t.nodes[node]['leaf'];selected.append(leaf);records.append(dict(context=int(context),leaf=leaf,route=route))
        if records:
            with (Path(self.policy.context_path)/'selection_paths.jsonl').open('a',encoding='utf-8') as f:
                for r in records:f.write(json.dumps(r,ensure_ascii=False)+'\n')
        return torch.tensor(selected,device=self.latent.device)
    def get_actions(self,deterministic=False):return self.sample_rows(deterministic=deterministic)
    def mode(self):return self.get_actions(True)  # greedy branches, not global MAP
    def sample(self):return self.get_actions(False)
    def log_prob(self,actions):
        logs=[]
        for row,(context,action) in enumerate(zip(self.contexts,actions.detach().cpu().numpy().reshape(-1))):
            if not context:logs.append(self.latent[row].sum()*0);continue
            t,_=tree(self.policy.context_path,int(context));terms=[]
            if not 0<=int(action)<t.leaves:raise ValueError('Action outside original complete support')
            for node,edge in t.paths[int(action)]:
                children=t.nodes[node]['children']
                if len(children)>1:
                    index=list(children).index(edge);terms.append(self.categorical(row,node).log_prob(torch.tensor(index,device=self.latent.device)))
            logs.append(torch.stack(terms).sum() if terms else self.latent[row].sum()*0)
        return torch.stack(logs)
    def entropy(self):return None  # SB3's supported sampled -log_prob approximation

class HierarchicalOptionPolicy(JointOptionPolicy):
    def __init__(self,*args,**kwargs):
        super().__init__(*args,**kwargs)
        encoder=HierarchicalFeatures(self.observation_space,self.context_path)
        self.features_extractor=encoder;self.pi_features_extractor=encoder;self.vf_features_extractor=encoder
        schedule=kwargs.get('lr_schedule',args[2] if len(args)>2 else lambda _:1e-4)
        self.optimizer=self.optimizer_class(self.parameters(),lr=schedule(1),**self.optimizer_kwargs)
    def _get_action_dist_from_latent(self,latent_pi):return HierarchicalDistribution(self,latent_pi,self._contexts)
