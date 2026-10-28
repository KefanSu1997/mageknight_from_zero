"""Two legal heroes share an original central token in real alternating turns."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
 steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='原魔力强化003:09实际支付基本色返同色晶并共享标记；开源005:09额外基本色原骰、主人得晶及可选重掷；原耐力蓝费移动4、决心蓝费格挡5及正式回合归还') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','双英雄构筑'));click('CampShade/Camp/Btn_Loadout','原牌构筑')
for _ in range(2):click('CampShade/Picker/Btn_NextPage','下一页')
click('CampShade/Picker/Btn_Select_39','零部队')
p='CampShade/DeedPicker/'
click('CampShade/Camp/Btn_ChooseCards','两英雄独立原牌');click(p+'Btn_ClearDeeds','原牌选择')
for card in ['000','001','007','008']:click(p+'Btn_ChooseDeed_basic_card_'+card,'原基础牌')
click(p+'Btn_DeedNext','下一页')
for card in ['014','016','021']:click(p+'Btn_ChooseDeed_basic_card_'+card,'原基础牌')
click(p+'Btn_DoneDeeds','确认');click('CampShade/Camp/Btn_SmallDeckHeroBuild','按原英雄技能构筑')
h='CampShade/HeroBuild/'
click(h+'Btn_Hero_Krang','Krang原魔力强化');click(h+'Btn_Level_2','合法等级2');click(h+'Btn_Skill_9','原互动技能');click(h+'Btn_AddCompanion','独立实际第一英雄');click(h+'Btn_Hero_Goldyx','Goldyx原魔力开源');click(h+'Btn_Skill_9','原互动技能');click(h+'Btn_Begin','真实双英雄',{'journey.players':2})
click('Tactic_day_1/Btn_Tactic_day_1','第一英雄战术');click('Tactic_day_4/Btn_Tactic_day_4','第二英雄战术');click('Btn_BeginTurn','Krang实际回合',{'hero.id':'Krang','blue':1})
def shared():
 click('Btn_JourneyTools','实际回合工具');click('JourneyTools/Btn_SharedSkills','中央共享区')
def close():
 click('SharedSkills/Btn_Back','返回');click('JourneyTools/Btn_CloseTools','回到旅程')
def endurance():
 click('Btn_JourneyHand','原手牌');click('JourneyHand/Btn_Hand_4','原耐力');click('JourneyHand/Btn_OutsideChoice_strong','蓝费移动4');click('JourneyHand/Btn_PlayOutside','真实蓝费与移动4',{'movement':4});click('JourneyHand/Btn_CloseHand','返回')
shared();click('SharedSkills/Btn_Own_0','原魔力强化');click('SharedSkills/Btn_Color_Blue','蓝色付款预选',capture=True);click('SharedSkills/Btn_Confirm','预选不产生资源',{'shared.count':0,'blue':1});click('JourneyTools/Btn_CloseTools','实际付款');endurance()
click('Btn_JourneyMap','移动消费者');click('JourneyMap/Btn_Hex_forest','白昼森林');click('JourneyMap/Btn_Move','支付移动3进入森林',{'movement':1,'blue':1,'shared.ids':'hero_skill_pool_003:09','journey.position':'1,0'},True);click('JourneyMap/Btn_CloseMap','返回');click('Btn_EndTurn','真实结束首回合',{'journey.completedTurns':1});click('Btn_BeginTurn','Goldyx实际回合',{'hero.id':'Goldyx','blue':1})
shared();click('SharedSkills/Btn_Token_0','原魔力强化中央蓝标记',capture=True);click('SharedSkills/Btn_Confirm','领取真实蓝色临时魔力',{'shared.count':0,'tokens.blue':1});close();endurance();click('Btn_JourneyMap','第二玩家消费者');click('JourneyMap/Btn_Hex_forest','森林');click('JourneyMap/Btn_Move','蓝标记供能耐力后实际移动',{'movement':1,'blue':1,'tokens.blue':0},True);click('JourneyMap/Btn_CloseMap','返回')
shared();click('SharedSkills/Btn_Own_0','原魔力开源',capture=True);click('SharedSkills/Btn_Confirm','选择不重掷原骰并发布',{'shared.ids':'hero_skill_pool_005:09'});close();click('Btn_EndTurn','Goldyx正式回合末',{'journey.completedTurns':2});click('Btn_BeginTurn','Krang第二实际回合',{'hero.id':'Krang'})
shared();click('SharedSkills/Btn_Token_0','Goldyx中央原开源');click('SharedSkills/Btn_Die_0','源中实际蓝骰');click('SharedSkills/Btn_Confirm','额外领取原蓝骰',{'tokens.blue':1,'shared.count':0,'source.claims':1},True);click('SharedSkills/Btn_ReturnKeep_0','选择原骰不重掷归还');close()
click('Btn_JourneyEncounter_0','实际兽人战斗');click('Inspector/Btn_Next','格挡阶段');click('Hand/Btn_Card_basic_card_016_0','原决心');click('Inspector/Btn_Choice_strong','原蓝费格挡5');click('Inspector/Btn_Play','额外蓝骰实际供能',{'tokens.blue':0,'blue':1,'block':5},True);click('Enemy_0/Btn_Target','原兽人攻击');click('Inspector/Btn_Resolve','真实格挡消费者')
for _ in range(3):click('Inspector/Btn_Next','正式后续阶段')
click('Inspector/Btn_EndTurn','真实回合末归还同骰',{'journey.completedTurns':3,'source.claims':0},True)
if '--from' in sys.argv:
 for step in steps[:int(sys.argv[sys.argv.index('--from')+1])]:step['after']=[];step['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps))
