"""Remote real CUDA four-episode integration gate. Never formal training."""
import os,sys,json
from pathlib import Path
os.environ.update(OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',BATTLE_RL_ENCODER_BATCH='4')
os.environ['PATH']='/root/autodl-tmp/dotnet:/root/autodl-tmp/mkvenv/bin:'+os.environ['PATH']
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
out='remote_green_enhanced300'
os.environ['BATTLE_RL_CONTEXT_STORE']=str(Path(out,'contexts').resolve())
os.environ['BATTLE_RL_TEXT_ASSETS']=str(Path('H:/MageKnight_RL_Runs/20261013_mmbert_pair/text_assets').resolve())
import train_sb3_atomic_entity_text as b
import structured_joint_policy as s
from atomic_transport_fast import install
import hierarchical_packed_cache as packed
import green_option_descriptions as descriptions
from entropy_token_value_policy import bind_reference
install();packed.install();descriptions.install()
b.BattleEnv=s.BattleEnv;b.BattleRecurrentPolicy=s.StructuredJointPolicy
b.memory_record=lambda *a:None
b.render=lambda *a:None
b.RecordedRecurrentPPO.episodes_per_update=128
original=b.RecordedRecurrentPPO.train
def train(self):
    bind_reference(self)
    try:return original(self)
    finally:self.policy._entropy_reference=None
b.RecordedRecurrentPPO.train=train
sys.argv=['gate','--out',out,'--updates','300','--workers','32','--batch-size','128','--reward-scale','.01','--no-baseline','--evaluation-every','0','--dll','H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll','--train-pool','H:/MageKnight_RL_Runs/atomic_dataset/train.json','--eval-pool','H:/MageKnight_RL_Runs/atomic_dataset/eval.json','--text-assets',os.environ['BATTLE_RL_TEXT_ASSETS']]
import hashlib,psutil
for p in psutil.process_iter(['pid','cmdline']):
    if p.pid!=os.getpid() and any(Path(a).name in ('run_green_successor_gate.py','run_green_enhanced300.py','resume_public_state71.py') for a in (p.info['cmdline'] or [])[1:]):
        raise RuntimeError('Another trainer or gate is active')
gate=json.loads(Path('remote_green_successor_gate_retry/deployment_gate.json').read_text())
assert gate['passed']
for name in ('structured_joint_policy.py','structured_hierarchy_core.py','green_option_descriptions.py'):
    assert hashlib.sha256(Path('optimization_stage_20261014',name).read_bytes()).hexdigest()==gate['sources'][name]
def render(output,message):
    manifest=output/'manifest.json'
    if manifest.exists():
        data=json.loads(manifest.read_text());data.update(architecture='Green full structured entity state and numeric/text complete actions plus context-dependent hierarchical marker residual',fresh=True,evaluation_enabled=False,gate=gate,runner_sha=hashlib.sha256(Path(__file__).read_bytes()).hexdigest())
        manifest.write_text(json.dumps(data,indent=2))
    (output/'status.json').write_text(json.dumps(dict(pid=os.getpid(),message=message,time=__import__('time').time())))
b.render=render
b.main()


