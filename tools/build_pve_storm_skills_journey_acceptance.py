"""Legal Braevalar build: storm mana, movement, negotiation, recruit, next battle."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='原风暴独立二选一标记及每轮次数、原迷醉实际地点数值、真实原卡付款/移动/招募/战斗') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def hand():click('Btn_JourneyHand','同一真实手牌')
def closehand():click('JourneyHand/Btn_CloseHand','返回旅程')
def support():click('JourneyHand/Btn_Support','自身原技能')
def closesupport():click('JourneySupport/Btn_BackSupport','返回真实手牌')
def skill(i,option,expected):
    click('JourneySupport/Btn_OutsideSkill_'+str(i),'已学原技能');click('JourneySupport/Btn_OutsideOption_'+str(option),'原独立选择');click('JourneySupport/Btn_UseOutsideSkill','正式发动原技能',expected,True)
def play(i,choice,expected=None):
    click('JourneyHand/Btn_Hand_'+str(i),'实际原牌');click('JourneyHand/Btn_OutsideChoice_'+choice,'原效果');click('JourneyHand/Btn_PlayOutside','实际付款出牌',expected)
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','原英雄合法构筑'));click('CampShade/Camp/Btn_Loadout','复用七卡构筑起点');click('CampShade/Picker/Btn_NextPage','第二页');click('CampShade/Picker/Btn_NextPage','第三页');click('CampShade/Picker/Btn_Select_39','不预配部队')
p='CampShade/DeedPicker/'
click('CampShade/Camp/Btn_ChooseCards','原七牌');click(p+'Btn_ClearDeeds','清空旧选择')
for card in ['000','001','007','008']:click(p+'Btn_ChooseDeed_basic_card_'+card,'原牌 '+card)
click(p+'Btn_DeedNext','第二页')
for card in ['014','021','022']:click(p+'Btn_ChooseDeed_basic_card_'+card,'原牌 '+card)
click(p+'Btn_DoneDeeds','原牌构筑');click('CampShade/Camp/Btn_SmallDeckHeroBuild','通用原英雄技能构筑');click('CampShade/HeroBuild/Btn_Hero_Braevalar','Braevalar 自身原池');click('CampShade/HeroBuild/Btn_Level_6','原等级6可持三技能')
for i in [2,3,4]:click('CampShade/HeroBuild/Btn_Skill_'+str(i),'选择本英雄原技能')
click('CampShade/HeroBuild/Btn_Begin','保留合法等级与原技能开始',{'hero.id':'Braevalar','hero.level':6,'hero.skills':3,'fame':35},True)
click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','真实第一回合')
hand();support();skill(0,257,{'tokens.blue':1,'tokens.white':1,'journey.outsideSkillUsed':'True'});closesupport()
play(4,'strong',{'tokens.blue':0,'blue':1,'movement':4});closehand()
click('Btn_JourneyMap','实际地图');click('JourneyMap/Btn_Hex_plains','村庄');click('JourneyMap/Btn_Move','原移动消费2',{'movement':2,'journey.position':'0,1'});click('JourneyMap/Btn_CloseMap','返回')
click('Btn_JourneyInteraction','实际村庄');click('JourneyInteraction/Btn_BeginInteraction','正式交涉');click('JourneyInteraction/Btn_CloseInteraction','保留交涉')
hand();support();skill(2,0,{'influence':3});closesupport();play(3,'strong',{'tokens.white':0,'influence':7});closehand()
click('Btn_JourneyInteraction','真实供应');click('JourneyInteraction/Btn_Offer_army_2_006','原农民');click('JourneyInteraction/Btn_Recruit','原费用4真实招募',{'influence':3,'units.count':1},True);click('JourneyInteraction/Btn_EndInteraction','交涉结束清理余影响',{'influence':0});click('JourneyInteraction/Btn_CloseInteraction','返回');click('Btn_EndTurn','正式回合保留已招部队',{'journey.completedTurns':1,'units.count':1})
click('Btn_BeginTurn','第二回合同一英雄');hand();support();click('JourneySupport/Btn_OutsideSkill_0','雷云本轮仍已使用',{'journey.outsideSkillUsed':'True'});skill(1,257,{'tokens.green':1,'tokens.red':1});closesupport();play(0,'strong',{'tokens.green':0,'movement':4});closehand()
click('Btn_JourneyMap','继续同一地图');click('JourneyMap/Btn_Hex_camp','实际营地');click('JourneyMap/Btn_Move','移动2真实返回营地',{'movement':2,'journey.position':'0,0'});click('JourneyMap/Btn_CloseMap','返回')
click('Btn_JourneyEncounter_4','原狼骑兵')
for _ in range(3):click('Inspector/Btn_Next','正式战斗阶段')
click('Hand/Btn_Card_basic_card_021_0','原狂怒');click('Inspector/Btn_Choice_strong','原红费攻击4');click('Inspector/Btn_Play','消费闪电风暴红标，保留红晶',{'tokens.red':0,'red':1,'melee':4});click('Enemy_0/Btn_Target','实际原敌');click('Inspector/Btn_Resolve','原攻击4真实击杀',{'kills':1,'fame':38},True);click('Inspector/Btn_Next','战斗结束');click('Inspector/Btn_EndTurn','同英雄两回合保留实际部队与伤牌',{'journey.completedTurns':2,'journey.completedBattles':1,'units.count':1,'wounds':1,'hero.armor':3,'fame':38},True)
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
