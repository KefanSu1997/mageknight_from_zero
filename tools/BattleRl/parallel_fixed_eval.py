"""Batched GPU inference and parallel native stepping, task-local actor RNG."""
from concurrent.futures import ThreadPoolExecutor
import json
import numpy as np
import torch
from sb3_battle_env import BattleEnv


def collect(model, rows, history, output, workers=32):
    count=min(workers,len(rows));envs=[];active={};records=[];cursor=0
    shape=(model.policy.lstm_actor.num_layers,count,model.policy.lstm_actor.hidden_size)
    memory=tuple(torch.zeros(shape,device=model.device) for _ in range(2))
    def assign(i):
        nonlocal cursor
        if cursor==len(rows):return
        row=rows[cursor];cursor+=1
        envs[i].rows=[row]
        obs,_=envs[i].reset()
        gen=torch.Generator(device=model.device).manual_seed(int(row['task_id'][:8],16))
        active[i]=dict(obs=obs,start=True,rng=gen,row=row,total=0.,steps=0)
    try:
        for i in range(count):envs.append(BattleEnv(rows[:1],output,i,history,reward_config=getattr(model,'reward_config',None)))
        for i in range(count):assign(i)
        with ThreadPoolExecutor(max_workers=count) as pool, torch.no_grad():
            while active:
                ids=sorted(active)
                batch={k:np.stack([active[i]['obs'][k] for i in ids]) for k in active[ids[0]]['obs']}
                obs=model.policy.obs_to_tensor(batch)[0]
                starts=torch.tensor([active[i]['start'] for i in ids],device=model.device,dtype=torch.float32)
                dist,next_memory=model.policy.get_distribution(obs,tuple(m[:,ids,:] for m in memory),starts)
                actions=[int(torch.multinomial(dist.distribution.probs[j],1,generator=active[i]['rng']).item()) for j,i in enumerate(ids)]
                for m,n in zip(memory,next_memory):m[:,ids,:]=n
                futures=[pool.submit(envs[i].step,a) for i,a in zip(ids,actions)]
                for i,f in zip(ids,futures):
                    obs,reward,done,_,info=f.result();slot=active[i]
                    slot.update(obs=obs,start=False,total=slot['total']+reward,steps=slot['steps']+1)
                    if done:
                        records.append(dict(task_id=slot['row']['task_id'],reward=slot['total'],steps=slot['steps'],
                            won=info['is_success'],performance=info['performance']))
                        del active[i];assign(i)
                (output/'progress.json').write_text(json.dumps(dict(completed=len(records),total=len(rows),workers=count)))
        assert len(records)==len(rows) and len({r['task_id'] for r in records})==len(rows)
        order={r['task_id']:i for i,r in enumerate(rows)}
        return sorted(records,key=lambda r:order[r['task_id']])
    finally:
        for env in envs:env.close()
