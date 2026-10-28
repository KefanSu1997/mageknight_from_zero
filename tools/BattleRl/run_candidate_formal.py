"""Fresh candidate-aware PPO; fixed evaluation and early stopping against green."""
import os,sys,json,hashlib,shutil,time,argparse
from pathlib import Path
os.environ.update(OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',BATTLE_RL_ENCODER_BATCH='8')
os.environ['PATH']='/root/autodl-tmp/dotnet:/root/autodl-tmp/mkvenv/bin:'+os.environ['PATH']
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
p=argparse.ArgumentParser();p.add_argument('--out',required=True);p.add_argument('--gate',action='store_true');p.add_argument('--control-gate',action='store_true');p.add_argument('--workers',type=int,default=16);p.add_argument('--arm',choices=['dot','interaction'],required=True);a=p.parse_args();os.environ['BATTLE_RL_CANDIDATE_ARM']=a.arm
if sys.platform!='linux':raise RuntimeError('Remote GPU only')
out=Path(a.out)
os.environ['BATTLE_RL_CONTEXT_STORE']=str((out/'contexts').resolve())
os.environ['BATTLE_RL_TEXT_ASSETS']=str(Path('H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets').resolve())
import psutil
for proc in psutil.process_iter(['pid','cmdline']):
    if proc.pid!=os.getpid() and any(Path(x).name in ('run_final_semantic.py','run_green_enhanced300.py') for x in (proc.info['cmdline'] or [])[1:]):
        raise RuntimeError('Existing trainer: '+str(proc.pid))
sources=['candidate_option_policy.py','candidate_option_runtime.py','run_candidate_formal.py','candidate_green_baseline.json']
hashes={n:hashlib.sha256(Path('optimization_stage_20261014',n).read_bytes()).hexdigest() for n in sources}
import train_sb3_atomic_entity_text as b
import candidate_option_policy as s
from candidate_option_runtime import install_journal,runtime
from atomic_transport_fast import install
from entropy_token_value_policy import bind_reference
install();install_journal()
b.BattleEnv=s.BattleEnv;b.BattleRecurrentPolicy=s.CandidateOptionPolicy;b.memory_record=lambda *args:None
b.RecordedRecurrentPPO.episodes_per_update=4 if a.control_gate else 128
from ready_episode_collector import collect_rollouts
b.RecordedRecurrentPPO.collect_rollouts=collect_rollouts
old_init=b.RecordedRecurrentPPO.__init__
def configured_init(self,*args,**kwargs):
    if kwargs.get('policy_kwargs') is not None:kwargs['policy_kwargs'].update(candidate_arm=a.arm,context_path=os.environ['BATTLE_RL_CONTEXT_STORE'])
    old_init(self,*args,**kwargs)
b.RecordedRecurrentPPO.__init__=configured_init
original=b.RecordedRecurrentPPO.train
def train(self):
    assert shutil.disk_usage(out).free>3*2**30, "Disk guard: preserve checkpoint and stop"
    bind_reference(self)
    try:return original(self)
    finally:self.policy._entropy_reference=None
b.RecordedRecurrentPPO.train=train
def render(output,message):
    manifest=output/'manifest.json'
    if manifest.exists():
        data=json.loads(manifest.read_text());data.update(architecture='Full green structured candidate prior plus contextual option residual',candidate_arm=a.arm,fresh=True,evaluation_enabled=True,candidate_sources=hashes,episodes_per_update=4 if a.control_gate else 128,benchmark_only=False)
        manifest.write_text(json.dumps(data,indent=2))
        snapshot=output/'source_snapshot';snapshot.mkdir(exist_ok=True)
        for n in sources:
            if not (snapshot/n).exists():shutil.copy2(Path('optimization_stage_20261014',n),snapshot/n)
    rt=runtime(os.environ['BATTLE_RL_CONTEXT_STORE'])
    if rt.reader is not None:
        assert all(not p.requires_grad and p.grad is None for p in rt.reader.model.parameters())
    (output/'status.json').write_text(json.dumps(dict(pid=os.getpid(),created=psutil.Process().create_time(),message=message,time=time.time(),encoder_frozen=True)))
b.render=render
sys.argv=['fresh','--out',str(out),'--updates','2' if a.control_gate else '300','--workers',str(a.workers),'--batch-size','128','--reward-scale','.01','--evaluation-every','1' if a.control_gate else '20','--dll','H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll','--train-pool','H:/MageKnight_RL_Runs/atomic_dataset/train.json','--eval-pool','H:/MageKnight_RL_Runs/atomic_dataset/eval.json','--text-assets',os.environ['BATTLE_RL_TEXT_ASSETS']]
import parallel_hierarchical_eval as evaluation
evaluation.BattleEnv=s.BattleEnv
b.collect_fixed=lambda model,rows,history,output,workers=32:evaluation.collect(model,rows,history,output,workers=a.workers)
baseline=json.loads(Path('optimization_stage_20261014/candidate_green_baseline.json').read_text())
class BelowGreen(Exception):pass
original_eval=b.frozen_evaluation
def checked_eval(model,rows,history):
    assert shutil.disk_usage(out).free>3*2**30, "Disk guard before fixed evaluation"
    original_eval(model,rows[:4] if a.control_gate else rows,history)
    summary=json.loads((model.output/f'eval_{model.completed_updates:04d}/summary.json').read_text())
    step=model.completed_updates
    if not a.control_gate and 0<step<=150:
        ref=baseline[str(step)];assert summary['episodes']==ref['episodes']==1000
        comparison=dict(update=step,wins=summary['wins'],green_wins=ref['wins'],stop=summary['wins']<ref['wins'])
        with (model.output/'baseline_decisions.jsonl').open('a') as f:f.write(json.dumps(comparison)+'\n')
        if comparison['stop']:
            (model.output/'early_stop.json').write_text(json.dumps(comparison))
            raise BelowGreen()
b.frozen_evaluation=checked_eval
for arm in [a.arm]:
    folder=Path(f'remote_candidate_{arm}_gate')
    for filename in ['integration_gate.json','eval_gate.json']:
        gate_record=json.loads((folder/filename).read_text());assert gate_record['passed']
        if filename=='integration_gate.json':
            for name in ['candidate_option_policy.py','candidate_option_runtime.py']:
                assert hashlib.sha256(Path('optimization_stage_20261014',name).read_bytes()).hexdigest()==gate_record['sources'][name]
assert json.loads(Path('remote_candidate_dot_gate/text_gate.json').read_text())['passed']
try:b.main()
except BelowGreen:print('Stopped at complete fixed evaluation: below green',flush=True)
