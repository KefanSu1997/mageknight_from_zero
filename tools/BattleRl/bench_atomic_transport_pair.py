"""Interleaved real paired workload; same process, native worker and inputs."""
import json, os, time, hashlib, statistics, sys
from pathlib import Path
import numpy as np
from train_sb3_atomic_entity_text import configure_rules
from atomic_entity_text import BattleEnv
from atomic_transport_fast import install
from action_argument_features import catalog_argument_features
from train_ppo import Rules
from single_battle_reward import SingleBattleReward

root=Path(sys.argv[1] if len(sys.argv)>1 else 'H:/MageKnight_RL_Runs/20261013_transport_gate/interleaved')
root.mkdir(parents=True,exist_ok=False)
configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll')
os.environ['BATTLE_RL_TEXT_ASSETS']='H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets'
os.environ['BATTLE_RL_ATOMIC_REGISTRY']=str(root/'atomic_supports.jsonl')
rows=json.loads(Path('H:/MageKnight_RL_Runs/atomic_dataset/train.json').read_text())['tasks']; chosen={}
for row in rows:chosen.setdefault(row['hero'],row)
rules=Rules();history=catalog_argument_features(rules.actions);rules.close()
env=BattleEnv([next(iter(chosen.values()))],root,0,history,reward_config={'version':SingleBattleReward.version,'scale':.01})
records=[]
try:
    for n,(hero,row) in enumerate(chosen.items()):
        for fast in ([False,True] if n%2==0 else [True,False]):
            undo=install() if fast else None
            env.rows=[row];rng=np.random.default_rng(785);h=hashlib.sha256();start=time.perf_counter();obs,_=env.reset();steps=0
            while True:
                for k,v in sorted(obs.items()):
                    if k!='support_id':h.update(k.encode());h.update(v.tobytes())
                h.update(json.dumps(env.choices,sort_keys=True).encode())
                action=int(rng.integers(len(env.choices)))
                obs,reward,done,_,info=env.step(action);steps+=1
                t=dict(env.trace[-1]);t.pop('support_id');t.pop('worker_step')
                h.update(json.dumps([reward,done,t],sort_keys=True).encode())
                if done:break
            elapsed=time.perf_counter()-start
            records.append(dict(hero=hero,fast=fast,seconds=elapsed,steps=steps,sha=h.hexdigest()))
            print(json.dumps(records[-1]),flush=True)
            if undo:undo()
finally:env.close()
assert all(len({r['sha'] for r in records if r['hero']==h})==1 for h in chosen)
totals={str(f):sum(r['seconds'] for r in records if r['fast']==f) for f in (False,True)}
result=dict(records=records,totals=totals,speedup=totals['False']/totals['True'],equal=True)
(root/'gate.json').write_text(json.dumps(result,indent=2));print(json.dumps(totals),flush=True)
