import json
from pathlib import Path
from build_pve_manual_play_acceptance import build
base=build('attack');play=next(i for i,s in enumerate(base) if s['buttonPath'].endswith('Hand/Btn_Card_basic_card_021_0'))
steps=base[:play]
for s in steps:s['skipScreenshot']=True

def c(path,label,expected=None,shot=False):
 steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not shot,after=[dict(key=k,expected=str(v),rule='原阶段检查点恢复真实牌区/费用/伤牌/敌人/名望；当前及前阶段可重复悔棋') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
# Melee start contains the wounds already received in the previous phase.
c('Hand/Btn_Card_basic_card_021_0','选择原狂怒')
c('Inspector/Btn_Choice_strong','原强化')
c('Inspector/Btn_Play','实际支付红晶攻击4',{'red':0,'melee':4,'hand':5})
c('Enemy_0/Btn_Target','选择第一只原怪')
c('Inspector/Btn_Resolve','先击杀一只',{'kills':1,'fame':5,'melee':0},True)
c('Btn_ResetPhase','重置当前近战，恢复费用原牌敌人名望',{'phase':'Melee','hand':6,'realWounds':4,'red':1,'fame':3,'kills':0,'melee':0},True)
# Existing real two-target consumer checks after undo, with legal original hero skill.
steps.extend(base[play:])
c('Btn_ResetPhase','战果页重打近战，撤销两只敌人奖励',{'phase':'Melee','fame':3,'kills':0,'red':1,'hand':6,'melee':0})
for phase,expected in [('AssignDamage',{'phase':'AssignDamage','hand':2,'realWounds':0,'wounds':0,'red':1,'fame':3}),('Block',{'phase':'Block','hand':2,'red':1,'block':0}),('Ranged',{'phase':'Ranged','hand':2,'kills':0,'fame':3})]:
 c('Btn_Menu','打开阶段悔棋菜单')
 c('Btn_Reset_'+phase,'回到'+phase+'阶段起点',expected,phase=='Ranged')
# Block spend and undo is distinct from attack undo.
c('Inspector/Btn_Next','重新进入格挡')
c('Hand/Btn_Card_basic_card_000_0','选择实际行进')
c('Inspector/Btn_Sideways','横置正式格挡1',{'hand':1,'block':1})
c('Btn_ResetPhase','格挡悔棋恢复原牌及格挡池',{'hand':2,'block':0,'phase':'Block'})
c('Inspector/Btn_Next','重新进入承伤分配')
c('Btn_ResetPhase','重置承伤分配阶段起点',{'hand':2,'wounds':0,'phase':'AssignDamage'})
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=240,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'operations',sum(len(s['after']) for s in steps),'assertions')
