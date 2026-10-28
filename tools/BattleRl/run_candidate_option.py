"""AutoDL only: fresh semantic/rules final-token PPO, training-only by request."""
import os,sys,json,hashlib,shutil,time,argparse
from pathlib import Path
os.environ.update(OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',BATTLE_RL_ENCODER_BATCH='8')
os.environ['PATH']='/root/autodl-tmp/dotnet:/root/autodl-tmp/mkvenv/bin:'+os.environ['PATH']
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
p=argparse.ArgumentParser();p.add_argument('--out',required=True);p.add_argument('--gate',action='store_true');p.add_argument('--arm',choices=['dot','interaction'],required=True);a=p.parse_args();os.environ['BATTLE_RL_CANDIDATE_ARM']=a.arm
if sys.platform!='linux':raise RuntimeError('Remote GPU only')
out=Path(a.out)
os.environ['BATTLE_RL_CONTEXT_STORE']=str((out/'contexts').resolve())
os.environ['BATTLE_RL_TEXT_ASSETS']=str(Path('H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets').resolve())
import psutil
for proc in psutil.process_iter(['pid','cmdline']):
    if proc.pid!=os.getpid() and any(Path(x).name in ('run_final_semantic.py','run_green_enhanced300.py') for x in (proc.info['cmdline'] or [])[1:]):
        raise RuntimeError('Existing trainer: '+str(proc.pid))
sources=['candidate_option_policy.py','candidate_option_runtime.py','run_candidate_option.py']
hashes={n:hashlib.sha256(Path('optimization_stage_20261014',n).read_bytes()).hexdigest() for n in sources}
if not a.gate:
    gate=json.loads(Path('remote_candidate_'+a.arm+'_gate/deployment_gate.json').read_text())
    assert gate['passed'] and gate['sources']==hashes
import train_sb3_atomic_entity_text as b
import candidate_option_policy as s
from candidate_option_runtime import install_journal,runtime
from atomic_transport_fast import install
from entropy_token_value_policy import bind_reference
install();install_journal()
b.BattleEnv=s.BattleEnv;b.BattleRecurrentPolicy=s.CandidateOptionPolicy;b.memory_record=lambda *args:None
b.RecordedRecurrentPPO.episodes_per_update=4 if a.gate else 128
original=b.RecordedRecurrentPPO.train
def train(self):
    bind_reference(self)
    try:return original(self)
    finally:self.policy._entropy_reference=None
b.RecordedRecurrentPPO.train=train
def render(output,message):
    manifest=output/'manifest.json'
    if manifest.exists():
        data=json.loads(manifest.read_text());data.update(architecture='Full green structured candidate prior plus contextual option residual',candidate_arm=a.arm,fresh=True,evaluation_enabled=False,candidate_sources=hashes,episodes_per_update=4 if a.gate else 128)
        manifest.write_text(json.dumps(data,indent=2))
        snapshot=output/'source_snapshot';snapshot.mkdir(exist_ok=True)
        for n in sources:
            if not (snapshot/n).exists():shutil.copy2(Path('optimization_stage_20261014',n),snapshot/n)
    rt=runtime(os.environ['BATTLE_RL_CONTEXT_STORE'])
    if rt.reader is not None:
        assert all(not p.requires_grad and p.grad is None for p in rt.reader.model.parameters())
    (output/'status.json').write_text(json.dumps(dict(pid=os.getpid(),created=psutil.Process().create_time(),message=message,time=time.time(),encoder_frozen=True)))
b.render=render
sys.argv=['fresh','--out',str(out),'--updates','2' if a.gate else '300','--workers','4' if a.gate else '32','--batch-size','128','--reward-scale','.01','--no-baseline','--evaluation-every','0','--dll','H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll','--train-pool','H:/MageKnight_RL_Runs/atomic_dataset/train.json','--eval-pool','H:/MageKnight_RL_Runs/atomic_dataset/eval.json','--text-assets',os.environ['BATTLE_RL_TEXT_ASSETS']]
b.main()
