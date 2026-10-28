"""Only recheck corrected original enemy data/art; does not validate faction rewards."""
import argparse
import json
from pathlib import Path
from build_pve_wolfhawk_insight_element_acceptance import build as original

def build():
    steps=original()
    stop=next(i for i,s in enumerate(steps) if s['buttonPath'].endswith('/Btn_JourneyEncounter_7'))
    steps=steps[:stop+1]
    for s in steps:
        s['after']=[]
        s['skipScreenshot']=True
    final=steps[-1]
    final['buttonPath']='Canvas/BattleTable/Btn_JourneyEncounter_8'
    final['label']='原015双元素攻击与抗性保留，原阵营图案不得显示再生；奖励模式另行处理'
    final['skipScreenshot']=False
    final['after']=[dict(key=k,expected=str(v),rule='官方Tezla规则p2-3：名望右侧标志为阵营，不是再生能力；仅核本次数据标签改动') for k,v in {
        'ui.truncated':'','enemies.attacks':'3,3','enemies.armor':'4','wounds':0,'art.enemies':'True','ui.enemyArtCount':1
    }.items()]
    return steps

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--write',action='store_true');a=p.parse_args()
    steps=build()
    if a.write:Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=180,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
    print(len(steps),'total operations;',len(steps)-1,'replayed; 6 assertions, 1 visual review;', 'written' if a.write else 'plan only')
