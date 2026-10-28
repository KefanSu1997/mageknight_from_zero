"""Same farmer: movement, actual battle wounds, herbalist ready, paid healing."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='自有原部队移动真实消费、药草师重整同一实体、交涉影响力购买治疗消费实际伤牌') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def hand():click('Btn_JourneyHand','实际手牌')
def closehand():click('JourneyHand/Btn_CloseHand','返回当前旅程')
def units():click('JourneyHand/Btn_OwnedUnits','同一自有部队',{'ui.outsideUnitArtCount':2})
def closeunits():click('OwnedUnits/Btn_Close','保留真实部队状态')
def ability(unit,index):
    click('OwnedUnits/Btn_Unit_'+str(unit),'选择实际部队');click('OwnedUnits/Btn_Ability_'+str(index),'选择原能力')
def advance():click('Inspector/Btn_Next','推进正式战斗阶段')

(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','合法等级3双指挥槽'));click('CampShade/Camp/Btn_Loadout','原构筑')
for _ in range(3):click('CampShade/Picker/Btn_NextPage','下一页')
click('CampShade/Picker/Btn_Select_60','等级3原农民与暴徒')
click('CampShade/Camp/Btn_ChooseUnits','两槽自定已招募部队');click('CampShade/UnitPicker/Btn_Unit_army_2_011','移除暴徒');click('CampShade/UnitPicker/Btn_Unit_army_2_002','加入原药草师');click('CampShade/UnitPicker/Btn_Back','保留农民和药草师')
p='CampShade/DeedPicker/'
click('CampShade/Camp/Btn_ChooseCards','七张原牌');click(p+'Btn_ClearDeeds','清空旧选择')
for card in ['basic_card_000','basic_card_001','basic_card_002','basic_card_003','basic_card_008']:click(p+'Btn_ChooseDeed_'+card,'原卡 '+card)
click(p+'Btn_DeedNext','基础第二页');click(p+'Btn_ChooseDeed_basic_card_021','原狂怒');click(p+'Btn_DeedNext','基础第三页');click(p+'Btn_ChooseDeed_basic_card_026','原无情威压');click(p+'Btn_DoneDeeds','确认原构筑')
click('CampShade/Camp/Btn_Journey','同一玩家旅程');click('Tactic_day_1/Btn_Tactic_day_1','捷足先登');click('Btn_BeginTurn','真实第一回合',{'units.count':2,'units.ready':'True,True','fame':8})
hand();units();ability(0,2);click('OwnedUnits/Btn_Use','原农民移动2并耗竭',{'movement':2,'units.ready':'False,True'},True);closeunits();closehand()
click('Btn_JourneyMap','实际地图');click('JourneyMap/Btn_Hex_plains','实际村庄');click('JourneyMap/Btn_Move','移动2支付村庄费用',{'movement':0,'journey.position':'0,1','journey.atVillage':'True'});click('JourneyMap/Btn_CloseMap','返回')
click('Btn_JourneyEncounter_4','实际狼骑兵战斗');advance();advance();advance()
click('Hand/Btn_Card_basic_card_021_0','原狂怒');click('Inspector/Btn_Choice_attack','原攻击2');click('Inspector/Btn_Play','原卡正式出牌')
for card in ['basic_card_000','basic_card_002']:
    click('Hand/Btn_Card_'+card+'_0','实际原牌横置');click('Inspector/Btn_Sideways','实际攻击加1')
click('Enemy_0/Btn_Target','原狼骑兵');click('Inspector/Btn_Resolve','攻击4真实击杀，保留实际伤牌',{'kills':1,'fame':11,'wounds':2});advance()
click('Inspector/Btn_EndTurn','正式回合结束不重建农民',{'journey.completedTurns':1,'units.ready':'False,True','wounds':2})
click('Btn_BeginTurn','第二回合同一玩家与部队');click('Btn_JourneyInteraction','村庄交涉');click('JourneyInteraction/Btn_BeginInteraction','正式交涉窗口');click('JourneyInteraction/Btn_CloseInteraction','保留交涉')
hand();units();ability(1,1);click('OwnedUnits/Btn_Target_0','重整目标为同一耗竭农民');click('OwnedUnits/Btn_Use','原药草师重整，不改变创伤',{'units.ready':'True,False','units.wounds':'0,0','wounds':2},True)
ability(0,1);click('OwnedUnits/Btn_Use','同一农民交涉影响力2',{'influence':2,'units.ready':'False,False'},True);closeunits()
# Remaining originals: tranquility, wild harvest, promise, intimidation, then actual wounds.
click('JourneyHand/Btn_Hand_2','原承诺');click('JourneyHand/Btn_OutsideChoice_base','原影响力2');click('JourneyHand/Btn_PlayOutside','实际原卡叠加到4',{'influence':4});closehand()
click('Btn_JourneyInteraction','实际村庄');click('JourneyInteraction/Btn_BuyHealing','影响力3购买治疗1',{'influence':1,'healing':1});click('JourneyInteraction/Btn_CloseInteraction','返回同一交涉')
hand();click('JourneyHand/Btn_HealHero','消费治疗，真实伤牌归供应',{'healing':0,'wounds':1},True)
click('JourneyHand/Btn_Hand_0','保留到第二回合的原宁静');click('JourneyHand/Btn_OutsideChoice_heal','原治疗1');click('JourneyHand/Btn_PlayOutside','原治疗进入真实池');click('JourneyHand/Btn_HealHero','另一张真实伤牌移除',{'wounds':0,'healing':0});closehand()
click('Btn_EndTurn','两回合保留实际两部队耗竭和无伤结果',{'journey.completedTurns':2,'journey.completedBattles':1,'units.ready':'False,False','wounds':0,'fame':11},True)
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
