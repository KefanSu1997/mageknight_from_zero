"""Focused original-card UI and deferred gold consumption; no runtime state injection."""
import json
from pathlib import Path
from build_pve_manual_play_acceptance import build
base=build('attack')
cut=next(i for i,s in enumerate(base) if s['buttonPath'].endswith('Tactic_day_1/Btn_Tactic_day_1'))
steps=base[:cut]
extra=lambda path,label:dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=True,after=[])
i=next(i for i,s in enumerate(steps) if s['buttonPath'].endswith('Btn_ChooseDeed_basic_card_000'))
steps[i+1:i+1]=[extra('CampShade/DeedPicker/Btn_ChooseDeed_basic_card_002','原凝结')]
i=next(i for i,s in enumerate(steps) if s['buttonPath'].endswith('Btn_DeedNext'))
steps[i+1:i+1]=[extra('CampShade/DeedPicker/Btn_ChooseDeed_basic_card_015','原晶化')]
for s in steps:
 for a in s['after']:
  if a['key']=='hand':a['expected']='4'
def c(path,label,state=None,shot=False):
 steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not shot,after=[dict(key=k,expected=str(v),rule='原卡模式分层、金色延迟按实际原费用消费，未混用基础强化') for k,v in {'ui.truncated':'',**(state or {})}.items()]))
c('Tactic_day_1/Btn_Tactic_day_1','原战术')
c('Btn_BeginTurn','正式回合')
c('Btn_JourneyTools','实际公共源',{'source.available':'Gold,White,Green'})
c('JourneyTools/Btn_Source_0','选择实际金骰')
c('JourneyTools/Btn_UseSource','取出仍是金色',{'tokens.gold':1,'tokens.red':0},True)
c('JourneyTools/Btn_CloseTools','返回')
c('Btn_JourneyEncounter_2','真实双原怪')
c('Hand/Btn_Card_basic_card_015_0','晶化默认明确基础',{'ui.cardMode':'base'},True)
c('Inspector/Btn_Choice_base_Red','基础红色转晶')
c('Inspector/Btn_Play','金色在付款时用于红色',{'red':2,'tokens.gold':0,'tokens.red':0,'hand':3})
c('Btn_ResetPhase','恢复同场实际状态',{'red':1,'tokens.gold':1,'hand':4})
c('Hand/Btn_Card_basic_card_015_0','选择晶化')
c('Inspector/Btn_Choice_strong','先选择强化层',{'ui.cardMode':'strong'},True)
c('Inspector/Btn_Choice_strong_Red','再选择红晶')
c('Inspector/Btn_Play','金色在此用于蓝费，蓝晶保留',{'red':2,'blue':1,'tokens.gold':0,'hand':3})
c('Btn_ResetPhase','恢复')
c('Hand/Btn_Card_basic_card_002_0','凝结默认基础',{'ui.cardMode':'base'},True)
c('Inspector/Btn_Choice_red','本层红色标记')
c('Inspector/Btn_Play','基础凝结不支付强化费用',{'tokens.red':1,'tokens.gold':1,'hand':3})
c('Btn_ResetPhase','恢复原牌及真实库存')
for _ in range(3):c('Inspector/Btn_Next','正式进入近战')
c('Hand/Btn_Card_basic_card_002_0','选择凝结')
c('Inspector/Btn_Choice_strong','凝结原强化入口',{'ui.cardMode':'strong'})
c('Inspector/Btn_Play','金色用于原绿费',{'tokens.gold':0})
c('Hand/Btn_Card_basic_card_021_0','原狂怒自动选强化层',{'ui.cardMode':'strong'},True)
c('Inspector/Btn_Play','实际免费强化4加2，红晶未动',{'melee':6,'red':1})
c('Btn_ResetPhase','重置近战恢复两原牌与金标',{'tokens.gold':1,'melee':0})
c('Btn_Menu','菜单')
c('Btn_Camp','返回配置，供继续试玩')
r=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=240,reportPath='AutomationOutputs/PveBattle/card_modes_20261001_final/report.json',screenshotsDirectory='AutomationOutputs/PveBattle/card_modes_20261001_final/captures',useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_card_modes.json').write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'operations',sum(len(s['after']) for s in steps),'assertions')
