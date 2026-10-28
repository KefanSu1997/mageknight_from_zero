"""Frozen final-token evaluation using real checkpoint and task-local RNG."""
import os,sys,json,time,hashlib,argparse
from pathlib import Path
os.environ.update(OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',BATTLE_RL_ENCODER_BATCH='8')
os.environ['PATH']='/root/autodl-tmp/dotnet:/root/autodl-tmp/mkvenv/bin:'+os.environ['PATH']
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
p=argparse.ArgumentParser();p.add_argument('--arm',choices=['dot','interaction'],required=True);p.add_argument('--checkpoint',required=True);p.add_argument('--out',required=True);p.add_argument('--update',type=int,required=True);p.add_argument('--limit',type=int,default=1000);p.add_argument('--workers',type=int,default=32);a=p.parse_args()
os.environ['BATTLE_RL_CANDIDATE_ARM']=a.arm
out=Path(a.out);out.mkdir(parents=True,exist_ok=False)
os.environ.update(BATTLE_RL_CONTEXT_STORE=str((out/'contexts').resolve()),BATTLE_RL_ATOMIC_REGISTRY=str((out/'atomic_supports.jsonl').resolve()),BATTLE_RL_TEXT_ASSETS=str(Path('H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets').resolve()))
import torch,numpy as np
import train_sb3_atomic_entity_text as b
import parallel_hierarchical_eval as evaluation
from candidate_option_policy import BattleEnv
from candidate_option_runtime import install_journal
from atomic_transport_fast import install
from train_ppo import Rules
from action_argument_features import catalog_argument_features
sha=lambda p:hashlib.sha256(Path(p).read_bytes()).hexdigest()
manifest=json.loads((Path(a.checkpoint).parent/'manifest.json').read_text())
for name,h in manifest['candidate_sources'].items():assert sha(Path('optimization_stage_20261014')/name)==h
torch.set_num_threads(1);b.configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll');install();install_journal();evaluation.BattleEnv=BattleEnv
assert sha(os.environ['BATTLE_RL_DLL'])==manifest['rules_sha']
pool=Path('H:/MageKnight_RL_Runs/atomic_dataset/eval.json');assert sha(pool)==manifest['eval_sha']
src=json.loads(pool.read_text());rows=(src['tasks']+src.get('unknown_tasks',[]))[:a.limit]
assert len(rows)==a.limit and len({r['task_id'] for r in rows})==a.limit
r=Rules()
try:history=catalog_argument_features(r.actions)
finally:r.close()
model=b.RecordedRecurrentPPO.load(a.checkpoint,device='cuda')
assert model.completed_updates==a.update
model.policy.context_path=os.environ['BATTLE_RL_CONTEXT_STORE'];model.policy.registry_file=os.environ['BATTLE_RL_ATOMIC_REGISTRY']
model.reward_config={'version':b.SingleBattleReward.version,'scale':.01};model.policy.set_training_mode(False)
before=b.parameter_sha(model.policy);rng=b.rng_state();start=time.perf_counter()
records=evaluation.collect(model,rows,history,out,workers=a.workers)
b.restore_rng(rng);assert b.parameter_sha(model.policy)==before
result=dict(update=a.update,episodes=len(records),wins=sum(x['won'] for x in records),mean_reward=float(np.mean([x['reward'] for x in records])),performance=b.performance_metrics([x['performance'] for x in records]),frozen_parameters=True,training_rng_restored=True,per_task_actor_seed=True,checkpoint=str(a.checkpoint),checkpoint_sha=sha(a.checkpoint),parameter_sha=before,seconds=time.perf_counter()-start,records=records,scope='fixed1000' if a.limit==1000 else 'integration_gate_only')
(out/'summary.json').write_text(json.dumps(result,indent=2));print(json.dumps({k:v for k,v in result.items() if k!='records'}),flush=True)
