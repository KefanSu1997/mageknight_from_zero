"""Fixed checkpoint/task RNG, real 32-environment CUDA evaluation comparison."""
import os,json,time,gc,hashlib,sys
from pathlib import Path
import torch
import train_sb3_atomic_entity_text as base
from train_ppo import Rules
from action_argument_features import catalog_argument_features
from atomic_entity_text import BattleEnv
import parallel_atomic_eval as old_eval
import parallel_atomic_eval_fast as fast_eval
from atomic_transport_fast import install

root=Path(sys.argv[1] if len(sys.argv)>1 else 'H:/MageKnight_RL_Runs/20261013_transport_gate/cuda32')
root.mkdir(parents=True,exist_ok=False)
parent=Path('H:/MageKnight_RL_Runs/20261013_transport_gate/pause180')
base.configure_rules('H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll')
os.environ['BATTLE_RL_TEXT_ASSETS']='H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets'
os.environ['BATTLE_RL_TEXT_ARM']='text'
torch.set_num_threads(1)
model=base.RecordedRecurrentPPO.load(parent/'checkpoint.zip',device='cuda')
model.policy.set_training_mode(False)
state=torch.load(parent/'continuation.pt',weights_only=False,map_location='cpu')
assert model.completed_updates==state['completed_updates']==180
assert all(torch.isfinite(v).all() for s in model.policy.optimizer.state.values() for v in s.values() if torch.is_tensor(v))
binding=dict(update=180,parameter_sha=base.parameter_sha(model.policy),adam_entries=len(model.policy.optimizer.state),optimizer_updates=model._n_updates)
(root/'binding.json').write_text(json.dumps(binding));del state;gc.collect()
rules=Rules();history=catalog_argument_features(rules.actions);rules.close()
rows=json.loads(Path('H:/MageKnight_RL_Runs/atomic_dataset/eval.json').read_text())['tasks'][:64]
result=[]
for arm in (('fast','old') if '--reverse' in sys.argv else ('old','fast')):
    output=root/arm;output.mkdir();os.environ['BATTLE_RL_ATOMIC_REGISTRY']=str(output/'atomic_supports.jsonl')
    model.policy.registry_file=os.environ['BATTLE_RL_ATOMIC_REGISTRY']
    phase=dict(init=0.,reset=0.,step=0.)
    class TimedEnv(BattleEnv):
        def __init__(self,*a,**k):
            t=time.perf_counter();super().__init__(*a,**k);phase['init']+=time.perf_counter()-t
        def reset(self,**k):
            t=time.perf_counter();r=super().reset(**k);phase['reset']+=time.perf_counter()-t;return r
        def step(self,a):
            t=time.perf_counter();r=super().step(a);phase['step']+=time.perf_counter()-t;return r
    undo=install() if arm=='fast' else None
    module=fast_eval if arm=='fast' else old_eval;module.BattleEnv=TimedEnv
    start=time.perf_counter();records=module.collect(model,rows,history,output,workers=32);elapsed=time.perf_counter()-start
    row=dict(arm=arm,seconds=elapsed,phase_worker_seconds=phase,records=records,steps=sum(x['steps'] for x in records))
    result.append(row);(root/(arm+'.json')).write_text(json.dumps(row,indent=2))
    print(json.dumps({k:v for k,v in row.items() if k!='records'}),flush=True)
    if undo:undo()
    gc.collect()
assert result[0]['records']==result[1]['records']
assert base.parameter_sha(model.policy)==binding['parameter_sha']
timings={r['arm']:r['seconds'] for r in result}
summary=dict(equal=True,episodes=64,workers=32,old_seconds=timings['old'],fast_seconds=timings['fast'],speedup=timings['old']/timings['fast'],parameter_unchanged=True)
(root/'gate.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary),flush=True)
