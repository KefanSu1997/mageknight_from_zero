"""Original Tovak sideways choice with actual source history and formal turn consumers."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='魔法抗拒按实际源骰历史选择2或整回合承诺3，横置真实原牌一次并由正式攻击或移动消费') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','合法英雄构筑'));click('CampShade/Camp/Btn_Loadout','实际原牌配置')
for _ in range(2):click('CampShade/Picker/Btn_NextPage','下一页')
click('CampShade/Picker/Btn_Select_39','红蓝各一晶')
p='CampShade/DeedPicker/'
click('CampShade/Camp/Btn_ChooseCards','实际七牌');click(p+'Btn_ClearDeeds','清空旧构筑')
for c in ['000','001','007','008']:click(p+'Btn_ChooseDeed_basic_card_'+c,'选择原基础牌')
click(p+'Btn_DeedNext','下一页')
for c in ['014','021','022']:click(p+'Btn_ChooseDeed_basic_card_'+c,'选择原基础牌')
click(p+'Btn_DoneDeeds','确认');click('CampShade/Camp/Btn_SmallDeckHeroBuild','原英雄与技能');click('CampShade/HeroBuild/Btn_Hero_Tovak','托瓦克');click('CampShade/HeroBuild/Btn_Level_4','合法等级4');click('CampShade/HeroBuild/Btn_Skill_7','原魔法抗拒');click('CampShade/HeroBuild/Btn_Begin','同一真实玩家',{'hero.id':'Tovak','hero.skills':1})
click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','正式第一回合');click('Btn_JourneyEncounter_4','实际狼骑兵')
for _ in range(3):click('Inspector/Btn_Next','正式战斗阶段')
click('Btn_Skills','原技能');click('Skills/Btn_Skill_0','魔法抗拒');click('Inspector/Btn_UseSkill','实际手牌选择');click('SidewaysSkill/Btn_Card_4','实际耐力');click('SidewaysSkill/Btn_Value3','本回合承诺不取源骰',capture=True)
click('SidewaysSkill/Btn_Confirm','实际横置3与回合限制',{'melee':3,'source.forgone':'True','source.usedThisTurn':'False','skills.used':'True'},True)
click('Btn_Skills','返回原手牌');click('Hand/Btn_Card_basic_card_021_0','原狂怒');click('Inspector/Btn_Choice_attack','原攻击2');click('Inspector/Btn_Play','实际原牌攻击',{'melee':5});click('Enemy_0/Btn_Target','实际狼骑兵');click('Inspector/Btn_Resolve','真实攻击消费',{'kills':1,'source.forgone':'True'},True);click('Inspector/Btn_Next','结束实际战斗');click('Inspector/Btn_EndTurn','正式结束第一回合',{'journey.completedTurns':1},True)
click('Btn_BeginTurn','正式新回合解除承诺',{'source.forgone':'False','source.usedThisTurn':'False'})
click('Btn_JourneyTools','实际魔力源');click('JourneyTools/Btn_Source_0','原白骰');click('JourneyTools/Btn_UseSource','真实取骰并保留历史',{'tokens.white':1,'source.usedThisTurn':'True'},True);click('JourneyTools/Btn_CloseTools','返回')
click('Btn_JourneyHand','实际剩余手牌');click('JourneyHand/Btn_Support','原技能');click('JourneySupport/Btn_OutsideSkill_0','本回合魔法抗拒');click('JourneySupport/Btn_UseOutsideSkill','实际原牌横置');click('SidewaysSkill/Btn_Card_0','原行进');click('SidewaysSkill/Btn_Value2','已用源骰选择2',capture=True);click('SidewaysSkill/Btn_Confirm','横置原牌移动2',{'movement':2,'source.usedThisTurn':'True','source.forgone':'False'},True)
click('JourneySupport/Btn_BackSupport','返回');click('JourneyHand/Btn_CloseHand','真实地图');click('Btn_JourneyMap','相邻地形');click('JourneyMap/Btn_Hex_plains','实际村庄');click('JourneyMap/Btn_Move','支付实际地形费用',{'movement':0,'journey.position':'0,1'},True);click('JourneyMap/Btn_CloseMap','继续同玩家');click('Btn_EndTurn','正式两回合保留牌区',{'journey.completedTurns':2,'journey.completedBattles':1},True)
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
