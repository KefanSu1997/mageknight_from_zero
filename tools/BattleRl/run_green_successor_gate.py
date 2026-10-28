"""Remote real CUDA four-episode integration gate. Never formal training."""
import os,sys,json
from pathlib import Path
os.environ.update(OMP_NUM_THREADS='1',OPENBLAS_NUM_THREADS='1',BATTLE_RL_ENCODER_BATCH='4')
os.environ['PATH']='/root/autodl-tmp/dotnet:/root/autodl-tmp/mkvenv/bin:'+os.environ['PATH']
sys.path[:0]=['optimization_stage_20261014','optimization_stage_20261013','tools/BattleRl']
out='remote_green_successor_gate_retry'
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
b.RecordedRecurrentPPO.episodes_per_update=4
original=b.RecordedRecurrentPPO.train
def train(self):
    bind_reference(self)
    try:return original(self)
    finally:self.policy._entropy_reference=None
b.RecordedRecurrentPPO.train=train
sys.argv=['gate','--out',out,'--updates','1','--workers','4','--batch-size','128','--reward-scale','.01','--no-baseline','--evaluation-every','0','--dll','H:/MageKnight_RL_Runs/atomic_adapter_v2_frozen/BattleRl.dll','--train-pool','H:/MageKnight_RL_Runs/atomic_dataset/train.json','--eval-pool','H:/MageKnight_RL_Runs/atomic_dataset/eval.json','--text-assets',os.environ['BATTLE_RL_TEXT_ASSETS']]
b.main()

