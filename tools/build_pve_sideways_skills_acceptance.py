"""Original sideways skills, actual card/cost and consumers across formal turns."""
import json,sys
from pathlib import Path
hero=sys.argv[1] if len(sys.argv)>1 else 'Goldyx'
universal=hero=='Goldyx'
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='原横置技能以实际原牌类别/颜色与费用判值，仅所选牌生效；正式回合保留牌区和重置次数，移动/攻击真实消费') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','原技能合法构筑'));click('CampShade/Camp/Btn_Loadout','原牌起点')
for _ in range(2):click('CampShade/Picker/Btn_NextPage','下一页')
click('CampShade/Picker/Btn_Select_39','红蓝各一库存魔晶，无部队')
p='CampShade/DeedPicker/'
click('CampShade/Camp/Btn_ChooseCards','七张原牌');click(p+'Btn_ClearDeeds','清空旧选择')
for card in ['000','001','007','008']:click(p+'Btn_ChooseDeed_basic_card_'+card,'选择基础原牌')
click(p+'Btn_DeedNext','下一页')
for card in ['014','021']+(['022'] if universal else []):click(p+'Btn_ChooseDeed_basic_card_'+card,'选择基础原牌')
if not universal:
    click(p+'Btn_DeedKind_1','原高级行动');click(p+'Btn_ChooseDeed_advanced_card_000','实际高级行动')
click(p+'Btn_DoneDeeds','确认构筑');click('CampShade/Camp/Btn_SmallDeckHeroBuild','合法自身技能');click('CampShade/HeroBuild/Btn_Hero_'+hero,'原英雄');click('CampShade/HeroBuild/Btn_Level_4','等级4合法单技能');click('CampShade/HeroBuild/Btn_Skill_'+('7' if universal else '6'),'原自身技能');click('CampShade/HeroBuild/Btn_Begin','同一真实玩家',{'hero.id':hero,'hero.skills':1})
click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','正式第一回合');click('Btn_JourneyEncounter_4','原狼骑兵')
for _ in range(3):click('Inspector/Btn_Next','正式战斗阶段')
click('Btn_Skills','原横置技能',{'wounds':1});click('Skills/Btn_Skill_0','选择原技能');click('Inspector/Btn_UseSkill','选择实际原牌');click('SidewaysSkill/Btn_Card_'+('5' if universal else '4'),'真实红色狂怒' if universal else '真实基础行动',capture=True)
click('SidewaysSkill/Btn_Confirm','一次实际横置与费用',{'melee':4 if universal else 2,'red':0 if universal else 1,'skills.used':'True'},True)
click('Btn_Skills','原手牌')
if not universal:
    click('Hand/Btn_Card_basic_card_021_0','原狂怒基础');click('Inspector/Btn_Choice_attack','攻击2');click('Inspector/Btn_Play','原牌贡献',{'melee':4})
click('Enemy_0/Btn_Target','实际敌人');click('Inspector/Btn_Resolve','原攻击实际击杀',{'kills':1},True);click('Inspector/Btn_Next','战斗完成');click('Inspector/Btn_EndTurn','原牌进入实际弃牌堆',{'journey.completedTurns':1})
click('Btn_BeginTurn','同玩家第二回合');click('Btn_JourneyHand','保留原牌');click('JourneyHand/Btn_Support','原技能已重置');click('JourneySupport/Btn_OutsideSkill_0','同一自身技能');click('JourneySupport/Btn_UseOutsideSkill','战外选择真实手牌');click('SidewaysSkill/Btn_Card_'+'4','实际蓝色耐力' if universal else '实际高级行动',capture=True)
if universal:click('SidewaysSkill/Btn_Color_1','本次选实际蓝费')
click('SidewaysSkill/Btn_Confirm','同色行动4移动' if universal else '高级行动3移动',{'movement':4 if universal else 3,'blue':0 if universal else 1},True)
click('JourneySupport/Btn_BackSupport','返回');click('JourneyHand/Btn_CloseHand','地图');click('Btn_JourneyMap','实际地形');click('JourneyMap/Btn_Hex_plains','村庄');click('JourneyMap/Btn_Move','实际地形费用2',{'movement':2 if universal else 1,'journey.position':'0,1'},True);click('JourneyMap/Btn_CloseMap','旅程');click('Btn_EndTurn','连续两回合保留牌区',{'journey.completedTurns':2,'journey.completedBattles':1},True)
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf8')
print(hero,len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
