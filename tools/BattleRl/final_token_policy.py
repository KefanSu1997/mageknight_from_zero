"""Green structured encoder/LSTMs + single-final-token actor255 / critic1."""
import json,os
from pathlib import Path
import numpy as np
import torch
from torch import nn
from structured_joint_policy import BattleEnv as ContextEnv
from atomic_entity_text import EntityTextPolicy
from atomic_action_registry import registry
from final_token_option import semantic_document
from final_token_runtime import runtime

class SemanticRenderer:
    def __init__(self,renderer):self.renderer=renderer
    def render(self,*args):return semantic_document(self.renderer.render(*args))

class BattleEnv(ContextEnv):
    def __init__(self,*args,**kwargs):
        super().__init__(*args,**kwargs);self.renderer=SemanticRenderer(self.renderer)

def head(output):
    # Normalize each information source separately before fusion.
    return nn.Sequential(nn.Linear(512,128),nn.GELU(),nn.Linear(128,output))

class ValueReadout(nn.Module):
    def __init__(self):
        super().__init__();self.text_norm=nn.LayerNorm(384);self.state_norm=nn.LayerNorm(128);self.net=head(1);self.root=None
    def forward(self,latent):
        if self.root is None or len(self.root)!=len(latent):raise ValueError('Missing pre-action root context')
        return self.net(torch.cat((self.text_norm(self.root),self.state_norm(latent)),dim=-1))

class Distribution:
    def __init__(self,policy,latent):
        self.policy=policy;self.latent=latent;self.contexts=policy._contexts.copy();self.runtime=runtime(policy.context_path);self.memo={}
    def distributions(self,rows,nodes):
        requests=[(int(self.contexts[r]),n) for r,n in zip(rows,nodes)]
        v=torch.as_tensor(self.runtime.vectors(requests),device=self.latent.device)
        counts=[len(self.runtime.tree(c)[0].nodes[n]['children']) for c,n in requests]
        if any(k<1 or k>255 for k in counts):raise ValueError('Branch exceeds 255; no truncation allowed')
        logits=self.policy.node_logits(v,self.latent[rows])
        mask=torch.arange(255,device=logits.device)[None,:]<torch.tensor(counts,device=logits.device)[:,None]
        return torch.distributions.Categorical(logits=logits.masked_fill(~mask,-torch.inf))
    def sample_rows(self,generators=None,deterministic=False):
        selected=[0]*len(self.contexts);active={};records={}
        for row,c in enumerate(self.contexts):
            if c:
                t,_=self.runtime.tree(int(c));active[row]=(t,t.branch(0));records[row]=dict(context=int(c),route=[])
        while active:
            for row,(t,n) in list(active.items()):
                if t.nodes[n]['leaf'] is not None:selected[row]=t.nodes[n]['leaf'];records[row]['leaf']=selected[row];del active[row]
            if not active:break
            rows=list(active);nodes=[active[r][1] for r in rows];dist=self.distributions(rows,nodes)
            if deterministic:choices=dist.logits.argmax(-1)
            elif generators is None:choices=torch.multinomial(dist.probs,1).squeeze(-1)
            else:choices=torch.stack([torch.multinomial(dist.probs[i],1,generator=generators[r])[0] for i,r in enumerate(rows)])
            choices=choices.cpu().tolist()
            for j,(row,n,choice) in enumerate(zip(rows,nodes,choices)):
                self.memo[(row,n)]=dist.logits[j]
                t=active[row][0];edge,next_node=list(t.nodes[n]['children'].items())[choice]
                assert next_node>n
                records[row]['route'].append(dict(node=n,choice=choice,branch_count=len(t.nodes[n]['children']),locked=edge))
                active[row]=(t,t.branch(next_node))
        with (Path(self.policy.context_path)/'selection_paths.jsonl').open('a',encoding='utf-8') as f:
            for row in sorted(records):f.write(json.dumps(records[row],ensure_ascii=False)+'\n')
        return torch.tensor(selected,device=self.latent.device)
    def get_actions(self,deterministic=False):return self.sample_rows(deterministic=deterministic)
    def sample(self):return self.get_actions()
    def mode(self):return self.get_actions(True)
    def log_prob(self,actions):
        rows=[];nodes=[];choices=[]
        for row,(c,a) in enumerate(zip(self.contexts,actions.detach().cpu().numpy().reshape(-1))):
            if not c:continue
            t,_=self.runtime.tree(int(c))
            if not 0<=int(a)<t.leaves:raise ValueError('Action outside complete support')
            for node,edge in t.paths[int(a)]:
                children=t.nodes[node]['children']
                if len(children)>1:rows.append(row);nodes.append(node);choices.append(list(children).index(edge))
        result=self.latent.sum(-1)*0
        if not rows:return result
        if all((r,n) in self.memo for r,n in zip(rows,nodes)):
            logits=torch.stack([self.memo[(r,n)] for r,n in zip(rows,nodes)])
        else:logits=self.distributions(rows,nodes).logits
        terms=logits[torch.arange(len(rows),device=logits.device),torch.tensor(choices,device=logits.device)]
        return result.index_add(0,torch.tensor(rows,device=logits.device),terms)
    def entropy(self):return None

class FinalTokenPolicy(EntityTextPolicy):
    def __init__(self,*args,context_path=None,**kwargs):
        self.context_path=str(Path(context_path or os.environ['BATTLE_RL_CONTEXT_STORE']).resolve())
        super().__init__(*args,**kwargs)
        # Fixed slot output replaces leaf scorer; full structured state/entity and
        # history pathways are retained. Actor and critic have matching head shape.
        del self.argument_encoder
        self.action_net=head(255)
        self.actor_text_norm=nn.LayerNorm(384);self.actor_state_norm=nn.LayerNorm(128)
        self.value_net=ValueReadout()
        self.optimizer=self.optimizer_class(self.parameters(),lr=self.optimizer.param_groups[0]['lr'],**self.optimizer_kwargs)
    def node_logits(self,text,latent):
        return self.action_net(torch.cat((self.actor_text_norm(text),self.actor_state_norm(latent)),dim=-1))
    def _bind_mask(self,obs,padded=False):
        super()._bind_mask(obs,padded)
        self._contexts=obs['context_id'].detach().cpu().numpy().reshape(-1).astype('int64')
        rt=runtime(self.context_path)
        for c,s in zip(self._contexts,self._supports):
            if c:
                t,doc=rt.tree(int(c))
                if doc['support_id']!=int(s):raise ValueError('Context/support binding mismatch')
        requests=[(int(c),rt.root(int(c))) for c in self._contexts]
        self.value_net.root=torch.as_tensor(rt.vectors(requests),device=obs['context_id'].device)
    def _get_action_dist_from_latent(self,latent_pi):return Distribution(self,latent_pi)
    def predict_values(self,obs,*args,**kwargs):
        self._bind_mask(obs,padded=True)
        return super().predict_values(obs,*args,**kwargs)
    def evaluate_actions(self,obs,actions,*args,**kwargs):
        values,logp,entropy=super().evaluate_actions(obs,actions,*args,**kwargs)
        reference=getattr(self,'_entropy_reference',None)
        if reference is not None:
            selected=actions.detach().cpu().numpy().reshape(-1).astype('int64')
            old=torch.tensor([reference[(int(c),int(a))] if c else 0. for c,a in zip(self._contexts,selected)],device=logp.device,dtype=logp.dtype)
            entropy=-(logp-old).exp()*logp
        return values,logp,entropy
