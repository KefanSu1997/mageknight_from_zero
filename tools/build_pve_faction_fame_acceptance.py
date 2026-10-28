"""Replay the verified original-six-card route, check only new faction reward consumers."""
import argparse,json
from pathlib import Path
from build_pve_elusive_defend_acceptance import build as original_build

def build():
    steps=original_build()
    for step in steps:
        step['after']=[];step['skipScreenshot']=True
    expected={
        24:{'faction.mode':'TezlaJustFame','faction.fame':0,'faction.count':0,'fame':35},
        38:{'faction.fame':0,'faction.count':0,'fame':40,'kills':2},
        57:{'faction.fame':1,'faction.count':1,'fame':42,'kills':3,'wounds':0},
        58:{'faction.fame':1,'faction.count':1,'fame':42},
        59:{'fame':42,'journey.samePlayer':'True','journey.discardCount':6},
        64:{'fame':42,'hand':6,'red':0,'blue':0,'journey.samePlayer':'True'},
    }
    for i,values in expected.items():
        steps[i]['after']=[dict(key=k,expected=str(v),rule='官方Tezla p3 Just Fame，阵营额外1与原印刷名望/原利斧奖励分别结算；实际击杀仅一次，正式跨轮保留') for k,v in {'ui.truncated':'',**values}.items()]
    for i in [24,57,64]:steps[i]['skipScreenshot']=False
    return steps

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--write',action='store_true');p.add_argument('--from',dest='start',type=int,default=0);a=p.parse_args()
    steps=build()
    for s in steps[:a.start]:s['after']=[];s['skipScreenshot']=True
    if a.write:Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=480,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
    print(len(steps),'replay operations;',sum(bool(s['after']) for s in steps),'checked steps;',sum(len(s['after']) for s in steps),'assertions')
