"""Original mage mana pays a spell; herbalist green fee heals real battle wounds."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原部队产生魔力标记/晶体分离，原火球消费红标，原药草师支付绿费治疗实际伤牌') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def units():click('JourneyHand/Btn_OwnedUnits','同一真实部队原卡',{'ui.outsideUnitArtCount':2})
def ability(unit,index):
    click('OwnedUnits/Btn_Unit_'+str(unit),'实际原部队');click('OwnedUnits/Btn_Ability_'+str(index),'原能力')
def closeunits():click('OwnedUnits/Btn_Close','保留耗竭和真实资源')
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','等级3双指挥槽'));click('CampShade/Camp/Btn_Loadout','原构筑')
for _ in range(3):click('CampShade/Picker/Btn_NextPage','下一页')
click('CampShade/Picker/Btn_Select_60','起点等级3')
click('CampShade/Camp/Btn_ChooseUnits','合法两槽原部队')
for card in ['army_2_006','army_2_011']:click('CampShade/UnitPicker/Btn_Unit_'+card,'移除起点部队')
for card in ['army_007','army_2_002']:click('CampShade/UnitPicker/Btn_Unit_'+card,'加入原部队 '+card)
click('CampShade/UnitPicker/Btn_Back','保留烈焰法师与药草师')
p='CampShade/DeedPicker/'
click('CampShade/Camp/Btn_ChooseCards','原卡构筑');click(p+'Btn_ClearDeeds','清空旧选项')
for card in ['basic_card_000','basic_card_001','basic_card_003','basic_card_008']:click(p+'Btn_ChooseDeed_'+card,'原牌 '+card)
click(p+'Btn_DeedNext','第二页');click(p+'Btn_ChooseDeed_basic_card_021','原狂怒');click(p+'Btn_DeedKind_2','原法术');click(p+'Btn_ChooseDeed_magic_009','原火球');click(p+'Btn_DeedKind_3','原神器');click(p+'Btn_ChooseDeed_items_007','原翡翠戒指');click(p+'Btn_DoneDeeds','七张实际原牌')
click('CampShade/Camp/Btn_Journey','同一玩家连续旅程');click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','实际回合')
click('Btn_JourneyHand','实际手牌');units();ability(0,2);click('OwnedUnits/Btn_Use','战前原烈焰法师红标1与红晶1',{'tokens.red':1,'red':2,'units.ready':'False,True'},True);closeunits()
click('JourneyHand/Btn_NextHand','原手牌第二页');click('JourneyHand/Btn_Hand_6','原翡翠戒指');click('JourneyHand/Btn_ArtifactNormal','原正常效果');click('JourneyHand/Btn_PlayOutside','原绿标与库存绿晶分离',{'tokens.green':1,'green':1,'fame':9});click('JourneyHand/Btn_CloseHand','保留资源进入战斗')
click('Btn_JourneyEncounter_0','原兽人')
for _ in range(3):click('Inspector/Btn_Next','正式阶段与真实承伤')
click('Hand/Btn_Card_magic_009_0','原火球');click('Inspector/Btn_Choice_base','原红费火攻5');click('Inspector/Btn_Play','优先消费真实红标，保留库存红晶',{'tokens.red':0,'red':2,'wounds':2},True)
click('Enemy_0/Btn_Target','同一原兽人');click('Inspector/Btn_Resolve','真实火攻击杀',{'kills':1,'fame':11});click('Inspector/Btn_Next','战斗结束')
click('Btn_JourneyHand','同一战后手牌与真实伤牌');units();ability(1,0);click('OwnedUnits/Btn_Use','药草师支付原绿费治疗2并耗竭',{'tokens.green':0,'green':1,'healing':2,'units.ready':'False,False'},True);closeunits()
click('JourneyHand/Btn_HealHero','治疗第一张实际伤牌',{'wounds':1,'healing':1});click('JourneyHand/Btn_HealHero','治疗第二张实际伤牌',{'wounds':0,'healing':0},True);click('JourneyHand/Btn_CloseHand','保留实际恢复结果');click('Inspector/Btn_EndTurn','正式回合耗竭与库存保留',{'journey.completedTurns':1,'journey.completedBattles':1,'wounds':0,'red':2,'green':1,'units.ready':'False,False'},True)
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
