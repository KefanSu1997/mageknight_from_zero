"""Frozen contextual mmBERT, option-position readout, upstream recurrent PPO."""
import hashlib,json,os,threading
from collections import OrderedDict
from pathlib import Path
import gymnasium as gym
import numpy as np
import torch
from torch import nn
from stable_baselines3.common.torch_layers import BaseFeaturesExtractor
from sb3_contrib.common.recurrent.policies import RecurrentActorCriticPolicy
from sparse_battle_policy import GlobalLegalCategorical
from atomic_battle_env import BattleEnv as AtomicEnv
from joint_option_text import JointRenderer,tokenizer,encode_document,SNAPSHOT

_stores={};_reader=None;_store_lock=threading.RLock()
class ContextStore:
    def __init__(self,path):
        self.path=Path(path);self.path.mkdir(parents=True,exist_ok=True)
        self.lock=threading.RLock();self.offsets=[];self.cache=OrderedDict()
        self.journal=self.path/'contexts.jsonl'
        if self.journal.exists():
            with self.journal.open('rb') as f:
                while True:
                    pos=f.tell()
                    if not f.readline():break
                    self.offsets.append(pos)
    def add(self,doc):
        with self.lock:
            if len(self.offsets)>=2**24-1:raise ValueError('Context ID float precision capacity')
            payload=(json.dumps(doc,ensure_ascii=False,separators=(',',':'))+'\n').encode('utf-8')
            with self.journal.open('ab') as f:self.offsets.append(f.tell());f.write(payload)
            return len(self.offsets)
    def read(self,i):
        with self.lock:
            with self.journal.open('rb') as f:f.seek(self.offsets[i-1]);return json.loads(f.readline())
    def vectors(self,i):
        if i==0:return np.zeros((2,384),np.float32)
        if i in self.cache:self.cache.move_to_end(i);return self.cache[i]
        path=self.path/f'{i:08d}.npy'
        if path.exists():v=np.load(path,allow_pickle=False)
        else:
            global _reader
            if _reader is None:_reader=ContextReader()
            v=_reader.encode(self.read(i));np.save(path,v,allow_pickle=False)
        self.cache[i]=v
        while len(self.cache)>512:self.cache.popitem(last=False)
        return v
def store(path):
    path=str(Path(path).resolve())
    with _store_lock:
        if path not in _stores:_stores[path]=ContextStore(path)
        return _stores[path]

class ContextReader:
    def __init__(self):
        self.tok=tokenizer()
        from transformers import AutoModel
        self.model,loading=AutoModel.from_pretrained(SNAPSHOT,local_files_only=True,trust_remote_code=False,
            dtype=torch.float32,attn_implementation='sdpa',weights_only=True,output_loading_info=True)
        allowed={'decoder.weight','decoder.bias','head.norm.weight','head.dense.weight'}
        if loading.get('missing_keys') or loading.get('mismatched_keys') or set(loading.get('unexpected_keys',[]))-allowed:raise ValueError('Encoder loading mismatch')
        self.model.eval().requires_grad_(False).to(device='cuda',dtype=torch.bfloat16)
        import gc
        gc.collect()
        self.pretrained_context=self.model.config.max_position_embeddings
        # Default RoPE is evaluated at actual positions without changing weights.
        # >8192 is explicit positional extrapolation, not pretrained long-context
        # quality assurance. Resource gates must cover real largest supports.
        self.max_tokens=32768
        from joint_chunked_attention import enable
        enable(self.model)
    def encode(self,doc):
        ids,positions=encode_document(self.tok,doc,self.max_tokens)
        with torch.no_grad():
            x=torch.tensor([ids],device='cuda')
            hidden=self.model(input_ids=x,attention_mask={'full_attention':None,'sliding_attention':None}).last_hidden_state[0]
            vectors=hidden[[0]+positions].float().cpu().numpy()
        if not np.isfinite(vectors).all():raise ValueError('Nonfinite contextual vectors')
        return vectors

class BattleEnv(AtomicEnv):
    def __init__(self,*args,**kwargs):
        super().__init__(*args,**kwargs)
        self.context_store=store(os.environ['BATTLE_RL_CONTEXT_STORE'])
        self.renderer=JointRenderer(self.rules.info['featureNames'],os.environ['BATTLE_RL_TEXT_ASSETS'])
        self._context_cache=None
        self.observation_space.spaces['context_id']=gym.spaces.Box(0,2**24-1,(1,),np.float32)
    def _observation(self):
        obs=super()._observation()
        # Base constructor never calls this; formal resets can before choices.
        if not hasattr(self,'renderer') or self.closed_episode or not self.choices or self.support_id==0:
            obs['context_id']=np.zeros(1,np.float32);return obs
        key=(id(self.current),self.support_id,id(self.previous))
        if self._context_cache is None or self._context_cache[0]!=key:
            doc=self.renderer.render(self.current['features'],self.previous,self.choices)
            doc['support_id']=self.support_id
            doc['sequences']=[[self.key_to_id[a['Key']] for a in seq] for seq in self.choices]
            doc['operations']=self.choices
            self._context_cache=(key,self.context_store.add(doc),self.current,self.previous)
        obs['context_id']=np.array([self._context_cache[1]],np.float32);return obs

class JointFeatures(BaseFeaturesExtractor):
    def __init__(self,space,context_path):
        super().__init__(space,128);self.context_path=context_path
        self.projection=nn.Sequential(nn.LayerNorm(384),nn.Linear(384,128),nn.Tanh())
    def forward(self,obs):
        ids=obs['context_id'].detach().cpu().numpy().reshape(-1).astype('int64')
        vectors=np.stack([store(self.context_path).vectors(int(i))[0] for i in ids])
        return self.projection(torch.as_tensor(vectors,device=obs['context_id'].device))

class JointOptionPolicy(RecurrentActorCriticPolicy):
    def __init__(self,*args,context_path,registry_file,**kwargs):
        self.context_path=context_path;self.registry_file=registry_file
        kwargs.update(features_extractor_class=JointFeatures,features_extractor_kwargs={'context_path':context_path},
            ortho_init=False,net_arch=dict(pi=[128],vf=[128]),lstm_hidden_size=256,enable_critic_lstm=True,shared_lstm=False)
        super().__init__(*args,**kwargs)
        self.option_head=nn.Sequential(nn.LayerNorm(384),nn.Linear(384,128),nn.GELU())
        self.action_net=nn.Linear(128,128)
        self.option_bias=nn.Linear(128,1)
        schedule=kwargs.get('lr_schedule',args[2] if len(args)>2 else lambda _:1e-4)
        self.optimizer=self.optimizer_class(self.parameters(),lr=schedule(1),**self.optimizer_kwargs)
    def _bind(self,obs):self._contexts=obs['context_id'].detach().cpu().numpy().reshape(-1).astype('int64')
    def _get_action_dist_from_latent(self,latent_pi):
        values=[store(self.context_path).vectors(int(i))[1:] for i in self._contexts]
        width=max(map(len,values));device=latent_pi.device
        packed=np.zeros((len(values),width,384),np.float32);valid=torch.zeros((len(values),width),device=device,dtype=torch.bool)
        for i,v in enumerate(values):packed[i,:len(v)]=v;valid[i,:len(v)]=True
        option=self.option_head(torch.as_tensor(packed,device=device))
        logits=(self.action_net(latent_pi)[:,None]*option).sum(-1)/(128**.5)+self.option_bias(option).squeeze(-1)
        ids=torch.arange(width,device=device).expand(len(values),-1)
        return GlobalLegalCategorical(ids,valid,logits)
    def forward(self,obs,*args,**kwargs):self._bind(obs);return super().forward(obs,*args,**kwargs)
    def evaluate_actions(self,obs,*args,**kwargs):self._bind(obs);return super().evaluate_actions(obs,*args,**kwargs)
    def get_distribution(self,obs,*args,**kwargs):self._bind(obs);return super().get_distribution(obs,*args,**kwargs)
