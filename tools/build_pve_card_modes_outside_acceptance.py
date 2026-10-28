"""Representative shared outside/inputs modes, actual original Mana Draw consumer."""
import json,sys
from pathlib import Path
from build_pve_manual_play_acceptance import build
base=build('attack')
cut=next(i for i,s in enumerate(base) if s['buttonPath'].endswith('Tactic_day_1/Btn_Tactic_day_1'))
steps=base[:cut]
i=next(i for i,s in enumerate(steps) if s['buttonPath'].endswith('Btn_ChooseDeed_basic_card_000'))
steps.insert(i+1,dict(buttonPath='Canvas/BattleTable/CampShade/DeedPicker/Btn_ChooseDeed_basic_card_009',label='实际原魔力汲取',waitAfterSeconds=.3,skipScreenshot=True,after=[]))
for s in steps:
 for a in s['after']:
  if a['key']=='hand':a['expected']='3'
def c(path,label,state=None,shot=False):
 steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not shot,after=[dict(key=k,expected=str(v),rule='共用分层入口与原魔力汲取真实额外骰许可') for k,v in {'ui.truncated':'',**(state or {})}.items()]))
c('Tactic_day_1/Btn_Tactic_day_1','原战术')
c('Btn_BeginTurn','实际回合')
c('Btn_JourneyHand','战外原手牌')
c('JourneyHand/Btn_Hand_1','原魔力汲取默认基础',{'ui.cardMode':'base'},True)
c('JourneyHand/Btn_OutsideChoice_strong','先选强化层',{'ui.cardMode':'strong'},True)
c('JourneyHand/Btn_PlayOutside','打开实际输入',{'ui.cardMode':'strong'},True)
c('DeedInputs/Btn_InputChoice_base','输入窗口切回基础',{'ui.cardMode':'base'},True)
c('DeedInputs/Btn_ConfirmInputs','实际基础许可不用白晶',{'hand':2,'blue':1,'red':1})
c('JourneyHand/Btn_CloseHand','返回真实旅程')
c('Btn_JourneyTools','消费额外骰许可',{'source.available':'White,Blue,White'})
c('JourneyTools/Btn_Source_1','实际蓝骰')
c('JourneyTools/Btn_UseExtraSource','原效果实际额外取骰',{'tokens.blue':1,'source.ordinaryUsed':'False'},True)
c('JourneyTools/Btn_CloseTools','返回')
c('Btn_JourneyEncounter_2','进入实际战斗再返回配置')
c('Btn_Menu','菜单')
c('Btn_Camp','准备正常试玩')
if '--from' in sys.argv:
 for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=180,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'operations',sum(len(s['after']) for s in steps),'assertions')
