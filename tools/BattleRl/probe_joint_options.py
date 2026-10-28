import json,os,time
from pathlib import Path
import numpy as np
from train_sb3_atomic_entity_text import configure_rules
from atomic_battle_env import BattleEnv
from atomic_transport_fast import install
from train_ppo import Rules
from action_argument_features import catalog_argument_features
from single_battle_reward import SingleBattleReward
from joint_option_text import JointRenderer,tokenizer,encode_document

out=Path('H:/MageKnight_RL_Runs/20261013_joint_option_prepare');out.mkdir(exist_ok=True)
os.environ['BATTLE_RL_ATOMIC_REGISTRY']=str(out/'probe_supports.jsonl')
configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll');install()
tok=tokenizer();r=Rules();history=catalog_argument_features(r.actions)
renderer=JointRenderer(r.info['featureNames'],'H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets');r.close()
rows=json.loads(Path('H:/MageKnight_RL_Runs/atomic_dataset/train.json').read_text())['tasks'];heroes={}
for row in rows:heroes.setdefault(row['hero'],row)
env=BattleEnv([rows[0]],out/'probe_episodes',0,history,reward_config={'version':SingleBattleReward.version,'scale':.01})
records=[];examples=[]
try:
 for hero,row in heroes.items():
  env.rows=[row];env.reset();rng=np.random.default_rng(785)
  while not env.closed_episode:
   d=renderer.render(env.current['features'],env.previous,env.choices);ids,pos=encode_document(tok,d,max_tokens=None)
   records.append(dict(hero=hero,step=len(env.trace),tokens=len(ids),options=len(pos)))
   if len(examples)<2 or len(ids)>max(e['tokens'] for e in examples):
    name=f'context_{len(records):04d}.json';(out/name).write_text(json.dumps(d,ensure_ascii=False),encoding='utf-8');examples.append(dict(file=name,tokens=len(ids)))
   env.step(int(rng.integers(len(env.choices))))
finally:env.close()
result=dict(rows=records,max_tokens=max(r['tokens'] for r in records),over8192=sum(r['tokens']>8192 for r in records),examples=examples)
(out/'length_probe.json').write_text(json.dumps(result,indent=2));print(json.dumps({k:v for k,v in result.items() if k not in ('rows','examples')}),flush=True)
