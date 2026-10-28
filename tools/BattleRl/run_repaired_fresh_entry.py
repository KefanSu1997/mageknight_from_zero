"""Remote-only entry for the repaired fresh policy; no saved policy is loaded."""
import os,sys,runpy
from pathlib import Path
os.environ['PATH']='/root/autodl-tmp/dotnet:/root/autodl-tmp/mkvenv/bin:'+os.environ['PATH']
os.environ['OPENBLAS_NUM_THREADS']='1'
os.environ['BATTLE_RL_ENCODER_BATCH']='4'
stage=Path('optimization_stage_20261014')
sys.path[:0]=[str(stage.resolve()),str(Path('optimization_stage_20261013').resolve()),str(Path('tools/BattleRl').resolve())]
runpy.run_path(str(stage/'train_repaired_fresh.py'),run_name='__main__')
