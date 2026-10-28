"""Compile/source binding and recovery of the same delayed bridge request."""
import argparse,json,time,hashlib,shutil
from pathlib import Path
from capture_ui_design_pairs import editor_call,CaptureDemo
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'AutomationOutputs/RsiGame/ui_iterations/20261024_embedded_battle_v1/attempt63'
def delayed(path):
 for _ in range(90):
  if path.exists():
   r=json.loads(path.read_text(encoding='utf8'));assert r['success'],r;return r
  time.sleep(1)
 raise TimeoutError('Same pending bridge response still unavailable: '+str(path))
def status():
 try:return editor_call('status',timeout=45)
 except TimeoutError:
  folder=ROOT/'AutomationOutputs/UiFluency/design_review';q=json.loads((folder/'editor_request.json').read_text(encoding='utf8'));assert q['operation']=='status'
  return delayed(folder/'editor_responses'/(q['id']+'.json'))
def main():
 p=argparse.ArgumentParser();p.add_argument('--tag',required=True);p.add_argument('--out',type=Path);a=p.parse_args()
 out=a.out.resolve() if a.out else OUT
 out.mkdir(parents=True,exist_ok=True)
 src=[ROOT/'Assets/Scripts/Battle'/n for n in ['PveBattleController.EmbeddedBattle.cs','PveBattleController.SymbolicField.cs','PveBattleController.BattlefieldReadability.cs','PveBattleController.CombatKnowledge.cs','PveBattleController.ManaInventory.cs']]+[ROOT/'Assets/Scripts/SceneAutomation/StructuredUiPilot.Evaluation.cs']
 for _ in range(20):
  s=status()
  if not s['isCompiling'] and Path(s['runtimeAssemblyPath']).stat().st_mtime>=max(p.stat().st_mtime for p in src):break
  time.sleep(2)
 else:raise RuntimeError('Compile/source freshness not proven')
 assert s['actualConsoleErrorCount']==0,s
 dest=out/('source_'+a.tag);dest.mkdir()
 for p in src:shutil.copy2(p,dest/p.name)
 s['sources']=[dict(path=str(p),sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in src]
 (out/('compile_'+a.tag+'.json')).write_text(json.dumps(s,indent=2),encoding='utf8');print('Bound '+a.tag+' '+s['runtimeModuleVersionId'],flush=True)
 live=out/('live_'+a.tag)
 try:CaptureDemo(live)
 except TimeoutError:
  req=json.loads((live/'request.json').read_text(encoding='utf8'));delayed(live/'responses'/(req['id']+'.json'))
 print('Pilot ready '+a.tag,flush=True)
if __name__=='__main__':main()
