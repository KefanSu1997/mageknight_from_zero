"""Norowas: actual inspired cards, Rally same farmer, battle, next-turn negotiation healing."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='原激励实际牌库抽牌及魔力消费、重整同一部队再用、正式跨回合原交涉治疗') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def hand():click('Btn_JourneyHand','实际原手牌')
def closehand():click('JourneyHand/Btn_CloseHand','返回旅程')
def support():click('JourneyHand/Btn_Support','原英雄技能')
def closesupport():click('JourneySupport/Btn_BackSupport','返回手牌')
def skill(i):click('JourneySupport/Btn_OutsideSkill_'+str(i),'选择已学原技能')
def use(expected=None,capture=True):click('JourneySupport/Btn_UseOutsideSkill','正式技能效果',expected,capture)
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','可复用英雄构筑'));click('CampShade/Camp/Btn_Loadout','可重放资源起点')
for _ in range(2):click('CampShade/Picker/Btn_NextPage','下一页')
click('CampShade/Picker/Btn_Select_39','复用种子0，无预配部队')
click('CampShade/Camp/Btn_ChooseUnits','选择实际原部队');click('CampShade/UnitPicker/Btn_Unit_army_2_006','原农民');click('CampShade/UnitPicker/Btn_Back','确认部队')
click('CampShade/Camp/Btn_SmallDeckHeroBuild','原英雄构筑');click('CampShade/HeroBuild/Btn_Hero_Norowas','诺洛瓦斯');click('CampShade/HeroBuild/Btn_Level_6','等级6三技能')
for i in [2,3,8]:click('CampShade/HeroBuild/Btn_Skill_'+str(i),'选择原自身技能')
click('CampShade/HeroBuild/Btn_DeckSource','本英雄原16牌，复用可重放洗混')
click('CampShade/HeroBuild/Btn_Begin','正式旅程起点',{'hero.id':'Norowas','hero.level':6,'hero.armor':3,'hero.deckTotal':16,'hand':6,'deck.count':10,'units.count':1},True)
click('Tactic_day_1/Btn_Tactic_day_1','正式战术');click('Btn_BeginTurn','本人第一回合')
hand();click('JourneyHand/Btn_OwnedUnits','实际农民');click('OwnedUnits/Btn_Unit_0','选择农民');click('OwnedUnits/Btn_Ability_2','原移动2');click('OwnedUnits/Btn_Use','耗竭农民获得移动',{'movement':2,'units.ready':'False'});click('OwnedUnits/Btn_Close','返回')
support();skill(2);use({'hand':8,'deck.count':8,'tokens.white':1});closesupport()
click('JourneyHand/Btn_NextHand','激励抽入的实际原卡');click('JourneyHand/Btn_Hand_7','实际新抽魔力结晶');click('JourneyHand/Btn_SidewaysMove','消费新抽原牌横置移动',{'movement':3,'hand':7})
click('JourneyHand/Btn_PreviousHand','原手牌第一页');closehand()
click('Btn_JourneyMap','真实地图');click('JourneyMap/Btn_Hex_plains','实际村庄');click('JourneyMap/Btn_Move','支付原费用2',{'movement':1,'journey.position':'0,1'});click('JourneyMap/Btn_CloseMap','返回')
hand();support();skill(0);click('JourneySupport/Btn_SkillUnit_0','同一实际耗竭农民');click('JourneySupport/Btn_OutsideOption_0','重整而非治疗');use({'units.ready':'True','units.wounds':0});closesupport();closehand()
click('Btn_JourneyEncounter_4','原狼骑兵')
for _ in range(3):click('Inspector/Btn_Next','正式阶段推进')
click('Hand/Btn_Card_basic_card_007_0','实际原敏捷');click('Inspector/Btn_Choice_strong','原白费远程3');click('Inspector/Btn_Play','消费激励白标供本次攻击',{'ranged':3,'tokens.white':0});
click('Btn_Roster','实际自有部队');click('Units/Btn_Unit_0','刚重整的同一农民');click('Inspector/Btn_UnitAbility_0','原农民攻击2再次耗竭',{'melee':2,'units.ready':'False','wounds':1},True);click('Btn_Roster','实际原手牌')
click('Hand/Btn_Card_basic_card_021_1','激励抽入的第二张原狂怒');click('Inspector/Btn_Choice_attack','原攻击2');click('Inspector/Btn_Play','消费激励新抽牌',{'melee':4});click('Enemy_0/Btn_Target','原狼骑兵');click('Inspector/Btn_Resolve','原近战4加远程3击杀护甲4',{'kills':1,'fame':38},True);click('Inspector/Btn_Next','战斗结束');click('Inspector/Btn_EndTurn','正式保留农民耗竭和伤牌',{'journey.completedTurns':1,'units.ready':'False','wounds':1})
click('Btn_BeginTurn','同一英雄次回合');click('Btn_JourneyInteraction','所在实际村庄');click('JourneyInteraction/Btn_BeginInteraction','正式交涉');click('JourneyInteraction/Btn_CloseInteraction','保留交涉')
hand();support();skill(2);click('JourneySupport/Btn_OutsideSkill_1','公开谈判',{'journey.outsideSkillUsed':'False'});use({'influence':3});closesupport();closehand()
click('Btn_JourneyInteraction','实际交涉消费者');click('JourneyInteraction/Btn_BuyHealing','支付影响3获得治疗1',{'influence':0,'healing':1});click('JourneyInteraction/Btn_CloseInteraction','返回');hand();click('JourneyHand/Btn_HealHero','治疗同玩家上一战真实伤牌',{'wounds':0,'healing':0},True)
click('JourneyHand/Btn_Hand_0','本回合实际原牌');click('JourneyHand/Btn_SidewaysInfluence','实际横置参与交涉',{'influence':1});closehand();click('Btn_EndTurn','两回合真实牌库/部队保留',{'journey.completedTurns':2,'journey.completedBattles':1,'units.ready':'False','wounds':0,'fame':38},True)
if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from')+1])]:step['after']=[];step['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf8')
print(len(steps),'steps',sum(len(step['after']) for step in steps),'checks')
