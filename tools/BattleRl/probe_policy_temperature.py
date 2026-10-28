"""Paired frozen-policy diagnostic; same 64 fixed tasks and per-task actor RNG."""
import os,sys,json,time,shutil
from pathlib import Path
os.environ['PATH']='/root/autodl-tmp/dotnet:/root/autodl-tmp/mkvenv/bin:'+os.environ['PATH']
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
import psutil,torch,numpy as np
import train_sb3_atomic_entity_text as base
import hierarchical_option_policy as h
import hierarchical_packed_cache as packed
from action_argument_features import catalog_argument_features
from train_ppo import Rules
from atomic_transport_fast import install
import parallel_hierarchical_eval as pe
out=Path('optimization_stage_20261014/temperature_probe');out.mkdir(exist_ok=False);paused=[]
try:
 p=psutil.Process(59984);assert 'deploy_entropy_after_eval80.py' in ' '.join(p.cmdline())
 for q in [p]+p.children(recursive=True):paused.append((q.pid,q.create_time()));q.suspend()
 shutil.copyfile('remote_entropy300/checkpoint.zip',out/'checkpoint.zip')
 torch.set_num_threads(1);os.environ['BATTLE_RL_ENCODER_BATCH']='4';os.environ['BATTLE_RL_TEXT_ASSETS']='H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets'
 base.configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll');install();packed.install()
 r=Rules();history=catalog_argument_features(r.actions);r.close()
 pool=json.loads(Path('H:/MageKnight_RL_Runs/atomic_dataset/eval.json').read_text())['tasks'];rows=[pool[int(i)] for i in np.linspace(0,len(pool)-1,64,dtype=int)]
 m=base.RecordedRecurrentPPO.load(out/'checkpoint.zip',device='cuda');m.reward_config={'version':base.SingleBattleReward.version,'scale':.01};m.policy.set_training_mode(False)
 pe.BattleEnv=h.BattleEnv;sha=base.parameter_sha(m.policy);original=h.HierarchicalDistribution.categorical;reports=[]
 for temperature in [1.,2.,4.]:
  def categorical(self,row,node):
   raw=original(self,row,node)
   return torch.distributions.Categorical(logits=raw.logits/temperature)
  h.HierarchicalDistribution.categorical=categorical
  folder=out/str(temperature);folder.mkdir();contexts=str(folder/'contexts');os.environ['BATTLE_RL_CONTEXT_STORE']=contexts;os.environ['BATTLE_RL_ATOMIC_REGISTRY']=str(folder/'atomic_supports.jsonl');m.policy.context_path=contexts
  for e in (m.policy.features_extractor,m.policy.pi_features_extractor,m.policy.vf_features_extractor):e.context_path=contexts
  start=time.perf_counter();records=pe.collect(m,rows,history,folder,workers=32)
  report=dict(temperature=temperature,episodes=len(records),wins=sum(r['won'] for r in records),mean_reward=float(np.mean([r['reward'] for r in records])),actions=sum(r['steps'] for r in records),seconds=time.perf_counter()-start,parameter_unchanged=sha==base.parameter_sha(m.policy))
  assert report['parameter_unchanged'] and len(records)==64
  (folder/'records.json').write_text(json.dumps(records));reports.append(report);(out/'results.json').write_text(json.dumps(reports,indent=2));print(json.dumps(report),flush=True)
finally:
 for pid,created in reversed(paused):
  try:
   p=psutil.Process(pid)
   if p.create_time()==created:p.resume()
  except psutil.NoSuchProcess:pass
