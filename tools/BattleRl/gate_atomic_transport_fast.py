"""Real seven-hero paired paths, frozen DLL; no policy or live run mutation."""
import argparse
import hashlib
import json
import os
import time
from pathlib import Path
import numpy as np
from train_sb3_atomic_entity_text import configure_rules
from atomic_entity_text import BattleEnv
from action_argument_features import catalog_argument_features
from train_ppo import Rules

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--out',required=True);parser.add_argument('--fast',action='store_true')
    args=parser.parse_args();out=Path(args.out);out.mkdir(parents=True,exist_ok=False)
    configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll')
    os.environ['BATTLE_RL_TEXT_ASSETS']='H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets'
    os.environ['BATTLE_RL_ATOMIC_REGISTRY']=str(out/'atomic_supports.jsonl')
    if args.fast:
        from atomic_transport_fast import install
        install()
    rows=json.loads(Path('H:/MageKnight_RL_Runs/atomic_dataset/train.json').read_text())['tasks']
    chosen={}
    for row in rows:chosen.setdefault(row['hero'],row)
    rules=Rules();history=catalog_argument_features(rules.actions);rules.close()
    env=BattleEnv([next(iter(chosen.values()))],out,0,history,reward_config={'version':'single_battle_v1','scale':.01})
    # Use the same reward contract as the running experiment.
    from single_battle_reward import SingleBattleReward
    env.config={'version':SingleBattleReward.version,'scale':.01}
    records=[];seconds=0.;steps=0
    try:
        for hero,row in chosen.items():
            env.rows=[row];rng=np.random.default_rng(785)
            start=time.perf_counter();obs,_=env.reset();h=hashlib.sha256()
            while True:
                for k,v in sorted(obs.items()):h.update(k.encode());h.update(v.tobytes())
                h.update(json.dumps(env.choices,sort_keys=True).encode())
                action=int(rng.integers(len(env.choices)))
                obs,reward,done,_,info=env.step(action);steps+=1
                h.update(json.dumps([reward,done,info,env.trace[-1]],sort_keys=True).encode())
                if done:break
            seconds+=time.perf_counter()-start
            records.append({'hero':hero,'sha':h.hexdigest(),'steps':len(env.trace)})
    finally:env.close()
    result={'fast':args.fast,'records':records,'seconds':seconds,'steps':steps,'ms_per_action':seconds/steps*1000}
    (out/'gate.json').write_text(json.dumps(result,indent=2));print(json.dumps(result),flush=True)

if __name__=='__main__':main()
