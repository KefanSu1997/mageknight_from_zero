"""Real UI rejection followed by a different, successful contribution assignment."""
import json
from pathlib import Path
from build_pve_manual_play_acceptance import build

steps=build('attack')
first=next(i for i,s in enumerate(steps) if s['buttonPath'].endswith('Inspector/Btn_Resolve'))
steps=steps[:first]
toggle=next(s for s in steps if s['buttonPath'].endswith('AttackSelection/Btn_Part_1'))
toggle['buttonPath']='Canvas/BattleTable/AttackSelection/Btn_Part_0'
toggle['label']='保留强化狂怒，只尝试横置攻击1'
def click(path,label,expected,shot=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not shot,
        after=[dict(key=k,expected=str(v),rule='原规则书第9页6：攻击不足可补牌或改目标，不能提前消费实际攻击来源') for k,v in {'ui.truncated':'',**expected}.items()]))
click('Inspector/Btn_Resolve','横置1不足原032护甲3，保留全部实际来源',{'kills':0,'melee':7,'fame':3,'rejections':1,'attacks.selected':1,'attacks.reserved':2},True)
click('Inspector/Btn_AttackParts','重新选择来源',{})
click('AttackSelection/Btn_Part_0','改用真实狂怒4',{})
click('AttackSelection/Btn_Part_1','保留真实横置1',{'attacks.selected':1,'attacks.reserved':2})
click('AttackSelection/Btn_Done','确认新分配',{})
click('Inspector/Btn_Resolve','原狂怒4击败第一只原敌',{'kills':1,'melee':3,'fame':5,'rejections':1},True)
click('Enemy_1/Btn_Target','第二只实际原敌',{})
click('Inspector/Btn_Resolve','横置1加原技能2击败第二只',{'kills':2,'melee':0,'fame':7,'rejections':1},True)
click('Inspector/Btn_Next','正式结束战斗',{'won':'True','kills':2})
config=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=180,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(config,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'operations',sum(len(s['after']) for s in steps),'assertions')
