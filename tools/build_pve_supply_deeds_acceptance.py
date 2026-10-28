"""Real supplies feed learned and practiced cards into the persistent player and next round."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='原供应同实例、学习可选6影响力、修行同色材料和绿费、法术熔炉原供应颜色；实际新牌消费及换轮牌区守恒') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def outside(index,choice=None):
    click('JourneyHand/Btn_Hand_'+str(index),'选择实际手牌 '+str(index))
    if choice:click('JourneyHand/Btn_OutsideChoice_'+choice,'原模式 '+choice)
    click('JourneyHand/Btn_PlayOutside','原效果入口')
p='CampShade/DeedPicker/'
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','供应与原牌连续路线'));click('CampShade/Camp/Btn_Loadout','原资源与确定性供应');click('CampShade/Picker/Btn_NextPage','第二页');click('CampShade/Picker/Btn_NextPage','第三页');click('CampShade/Picker/Btn_Select_39','显式种子0')
click('CampShade/Camp/Btn_ChooseCards','七原牌');click(p+'Btn_ClearDeeds','清空待选')
for id in ['basic_card_000','basic_card_008']:click(p+'Btn_ChooseDeed_'+id,'原牌 '+id)
click(p+'Btn_DeedNext','基础第二页')
for id in ['basic_card_014','basic_card_021']:click(p+'Btn_ChooseDeed_'+id,'原牌 '+id)
click(p+'Btn_DeedKind_1','高级原卡');click(p+'Btn_DeedNext','高级第二页');click(p+'Btn_DeedNext','高级第三页')
for id in ['advanced_card_026','advanced_card_027','advanced_card_033']:click(p+'Btn_ChooseDeed_'+id,'原牌 '+id)
click(p+'Btn_DoneDeeds','开始同一构筑');click('CampShade/Camp/Btn_Journey','真实玩家与供应');click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','第一回合',{'supply.advanced':'advanced_card_005,advanced_card_022,advanced_card_003','supply.spells':'magic_004,magic_023,magic_000'})
click('Btn_JourneyHand','原手牌');click('JourneyHand/Btn_NextHand','第七张牌');outside(6);click('DeedInputs/Btn_InputOffer_0','实际绿色法术供给',capture=True);click('DeedInputs/Btn_ConfirmInputs','法术熔炉取得绿晶，法术仍留供应',{'green':1,'blue':1,'supply.spells':'magic_004,magic_023,magic_000'})
outside(4);click('JourneyHand/Btn_LearningSupply','学习先获影响力，购牌另行选择',{'influence':2,'learning.modes':1,'learning.used':'False'},True);click('LearningSupply/Btn_CloseLearning','先返回积累影响力');click('JourneyHand/Btn_CloseHand','回原魔力源')
click('Btn_JourneyTools','实际公共源');click('JourneyTools/Btn_Source_0','实际白骰');click('JourneyTools/Btn_UseSource','取得一枚真实白标',{'tokens.white':1});click('JourneyTools/Btn_CloseTools','回手牌');click('Btn_JourneyHand','实际手牌');outside(1,'strong')
click('JourneyHand/Btn_LearningSupply','原影响力累计6，选择实际供应',{'influence':6,'tokens.white':0});click('LearningSupply/Btn_InputOffer_1','选择原魔力风暴',capture=True);click('LearningSupply/Btn_BuyLearning','学习支付6，所选原牌进弃牌，实际供应补位',{'influence':0,'learning.used':'True','discard.ids':'advanced_card_022','hand':4},True)
outside(3,'strong');click('DeedInputs/Btn_InputMaterial_1','实际蓝色耐力材料');click('DeedInputs/Btn_InputOffer_0','当前仍在供应的蓝色寒冰之盾',capture=True);click('DeedInputs/Btn_ConfirmInputs','绿晶付款、蓝材料永久去除、同色原盾入手',{'green':0,'removed.ids':'basic_card_014','hand.ids':'basic_card_000,basic_card_021,advanced_card_005'},True)
click('JourneyHand/Btn_CloseHand','新取得原盾继续使用');click('Btn_JourneyEncounter_0','实际原敌人');click('Inspector/Btn_Next','正式格挡阶段');click('Hand/Btn_Card_advanced_card_005_0','实际取得的寒冰之盾');click('Inspector/Btn_Choice_strong','强化原盾');click('Inspector/Btn_Play','支付蓝晶得寒冰格挡3',{'blue':0,'block':3});click('Hand/Btn_Card_basic_card_000_0','另一张原手牌');click('Inspector/Btn_Sideways','横置加格挡1',{'block':4});click('Enemy_0/Btn_Target','原攻击4');click('Inspector/Btn_Resolve','正式格挡消费');click('Inspector/Btn_Next','正式承伤');click('Inspector/Btn_Next','真实新盾格挡后减甲，英雄无伤',{'wounds':0,'enemies.armor':'1'},True)
click('Hand/Btn_Card_basic_card_021_0','原狂怒');click('Inspector/Btn_Choice_attack','基础攻击');click('Inspector/Btn_Play','攻击2');click('Enemy_0/Btn_Target','原护甲已减到1');click('Inspector/Btn_Resolve','实际击杀',{'kills':1,'fame':2});click('Inspector/Btn_Next','完成战斗');click('Inspector/Btn_EndTurn','原牌弃牌与实际取得牌同存',{'hand':0,'journey.discardCount':8,'removed.ids':'basic_card_014','journey.completedTurns':1})
click('Btn_Announce','真实空库声明轮末');click('Btn_EndRound','正式洗混八张实际原牌',{'hand':0,'deck.count':8,'journey.dayPart':'Night'});click('Btn_PrepareRound','保留新牌和永久移除区，正式抽5留3',{'hand':5,'deck.count':3,'removed.ids':'basic_card_014','journey.discardCount':0,'blue':0,'green':0},True)
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
