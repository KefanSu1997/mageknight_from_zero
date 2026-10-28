"""Benchmark only: exact previous real trajectories, never a teacher for PPO."""
import json
from pathlib import Path
import torch
from final_token_policy import BattleEnv,Distribution

def install(model):
    docs={i:[] for i in range(32)};position={i:[0,0] for i in range(32)};owners={}
    files=list(Path('remote_final_speed_old/episodes').glob('*episode*.json'))
    for path in sorted(files,key=lambda p:int(p.stem.split('_')[-1])):
        doc=json.loads(path.read_text());docs[doc['worker']].append(doc)
    assert sum(map(len,docs.values()))==64 and all(docs.values())
    model.benchmark_worker_limits={w:len(v) for w,v in docs.items()}
    original_obs=BattleEnv._observation;original_step=BattleEnv.step
    def observation(self):
        obs=original_obs(self);c=int(obs['context_id'][0])
        if c:owners[c]=self.worker
        return obs
    def step(self,action):
        w=self.worker;episode,index=position[w];record=docs[w][episode]['trace'][index]
        assert self.current['state']==record['before']
        assert int(action)==record['action_index'] and self.choices[int(action)]==record['sequence']
        result=original_step(self,action)
        assert self.current['state']==record['after'] and result[1]==record['training_reward']
        assert self.trace[-1]['micro']==record['micro']
        position[w][1]+=1
        if result[2]:
            assert position[w][1]==len(docs[w][episode]['trace'])
            position[w]=[episode+1,0]
        return result
    def select(self,generators=None,deterministic=False):
        actions=[]
        for c in self.contexts:
            if not c:actions.append(0);continue
            w=owners[int(c)];episode,index=position[w]
            actions.append(docs[w][episode]['trace'][index]['action_index'])
        return torch.tensor(actions,device=self.latent.device)
    BattleEnv._observation=observation;BattleEnv.step=step;Distribution.sample_rows=select
    return position,docs
