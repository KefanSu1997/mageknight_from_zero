"""Rebuild 32 actual saved closed environments without taking a training step."""
import os,sys,json,time,hashlib
from pathlib import Path
from concurrent.futures import ThreadPoolExecutor
os.environ['OPENBLAS_NUM_THREADS']='1'
os.environ['PATH']='/root/autodl-tmp/dotnet:/root/autodl-tmp/mkvenv/bin:'+os.environ['PATH']
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import numpy as np
import torch
import train_sb3_atomic_entity_text as base
from hierarchical_option_policy import BattleEnv
from atomic_transport_fast import install
from train_ppo import Rules
from action_argument_features import catalog_argument_features
parent=Path('remote_repaired_trainonly300');root=Path('optimization_stage_20261014/stop61_native_gate');root.mkdir(exist_ok=False)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
before={n:sha(parent/n) for n in ('checkpoint.zip','continuation.pt','atomic_supports.jsonl','contexts/contexts.jsonl')}
os.environ.update(BATTLE_RL_TEXT_ASSETS='H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets',BATTLE_RL_ATOMIC_REGISTRY=str(root/'atomic_supports.jsonl'),BATTLE_RL_CONTEXT_STORE=str(root/'contexts'))
base.configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll');install()
state=torch.load(parent/'continuation.pt',weights_only=False,map_location='cpu');assert state['completed_updates']==61
assert all(s['closed_episode'] for s in state['environments'])
r=Rules()
try:history=catalog_argument_features(r.actions)
finally:r.close()
envs=[];start=time.perf_counter();checks=[]
try:
 def create(i):
  e=BattleEnv([state['environments'][i]['row']],root/'episodes',i,history,reward_config={'version':base.SingleBattleReward.version,'scale':.01})
  envs.append((i,e));return e
 create(0)
 with ThreadPoolExecutor(max_workers=4) as pool:list(pool.map(create,range(1,32)))
 def check(pair):
  i,e=pair;s=state['environments'][i];e.restore(s)
  assert e.current==s['current'],('current',i)
  assert e.trace==s['trace'] and e.micro_trace==s['micro_trace']
  assert e.results==s['results'] and e.rng.bit_generator.state==s['rng']
  assert e.episode==s['episode'] and e.total_steps==s['total_steps'] and e.closed_episode
  for k,v in e._observation().items():np.testing.assert_array_equal(v,state['last_obs'][k][i],err_msg=str((i,k)))
  print('restored',i,flush=True)
  return dict(worker=i,episode=e.episode,replayed_atomic_steps=len(e.trace),full_state_observation_rng_counts_equal=True)
 with ThreadPoolExecutor(max_workers=4) as pool:checks=list(pool.map(check,envs))
finally:
 for _,e in envs:e.close()
assert before=={n:sha(parent/n) for n in before}
report=dict(passed=True,completed=61,workers=sorted(checks,key=lambda x:x['worker']),seconds=time.perf_counter()-start,source_sha=before,scope='32 native saved episode replays and closed observations checked. No policy sampling, optimization, fixed evaluation, or production mutation.')
(root/'gate.json').write_text(json.dumps(report,indent=2));print(json.dumps(report),flush=True)
