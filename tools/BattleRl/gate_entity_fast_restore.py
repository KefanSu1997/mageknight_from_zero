"""Restore all 32 actual closed workers, compare full saved state and observations."""
import os,json,time
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
import numpy as np
import torch
import train_sb3_atomic_entity_text as base
from atomic_entity_text import BattleEnv
from atomic_transport_fast import install
from train_ppo import Rules
from action_argument_features import catalog_argument_features
root=Path('H:/MageKnight_RL_Runs/20261013_transport_gate/restore32');root.mkdir(parents=True,exist_ok=False)
os.environ['BATTLE_RL_TEXT_ASSETS']='H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets'
os.environ['BATTLE_RL_ATOMIC_REGISTRY']=str(root/'atomic_supports.jsonl')
base.configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll');install()
state=torch.load('H:/MageKnight_RL_Runs/20261013_transport_gate/pause180/continuation.pt',weights_only=False,map_location='cpu')
rules=Rules();history=catalog_argument_features(rules.actions);rules.close()
envs=[];start=time.perf_counter()
try:
    def create(i):return BattleEnv([state['environments'][i]['row']],root,i,history,reward_config={'version':base.SingleBattleReward.version,'scale':.01})
    envs.append(create(0))
    with ThreadPoolExecutor(max_workers=4) as pool:envs.extend(pool.map(create,range(1,32)))
    def check(i):
        s=state['environments'][i];e=envs[i];e.restore(s)
        assert e.current==s['current'],('current',i)
        assert e.trace==s['trace'] and e.micro_trace==s['micro_trace']
        assert e.results==s['results'] and e.rng.bit_generator.state==s['rng']
        assert e.episode==s['episode'] and e.total_steps==s['total_steps'] and e.closed_episode
        for k,v in e._observation().items():np.testing.assert_array_equal(v,state['last_obs'][k][i],err_msg=str((i,k)))
        return i
    with ThreadPoolExecutor(max_workers=4) as pool:checked=list(pool.map(check,range(32)))
finally:
    for e in envs:e.close()
result=dict(update=state['completed_updates'],checked_workers=checked,seconds=time.perf_counter()-start,full_states_observations_rng_counts_equal=True)
(root/'gate.json').write_text(json.dumps(result,indent=2));print(json.dumps(result),flush=True)
