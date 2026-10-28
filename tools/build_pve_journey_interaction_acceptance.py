"""Actual original influence -> village hire -> same unit battle -> paid heal/ready -> round."""
import json
import sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原村庄影响力招募/治疗、原卡费用与奖励；实际部队/手牌/供应跨回合保留') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def hand():click('Btn_JourneyHand','打开实际手牌')
def closehand():click('JourneyHand/Btn_CloseHand','返回当前回合')
def interaction():click('Btn_JourneyInteraction','部队供应与实际村庄')
def closeinteraction():click('JourneyInteraction/Btn_CloseInteraction','返回，保留当前交涉')
def advance():click('Inspector/Btn_Next','推进正式战斗阶段')
cards=['basic_card_000','basic_card_008','basic_card_012','basic_card_021','basic_card_026','advanced_card_014','advanced_card_019']
actual=cards[:]
def play(id,choice='base',expected=None,capture=False):
    i=actual.index(id)
    click('JourneyHand/Btn_Hand_'+str(i),'选择原卡 '+id)
    click('JourneyHand/Btn_OutsideChoice_'+choice,'选择原印刷效果')
    click('JourneyHand/Btn_PlayOutside','实际出牌 '+id,expected,capture)
    actual.remove(id)
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','合法连续演练起点'));click('CampShade/Camp/Btn_Loadout','选择原英雄构筑')
click('CampShade/Picker/Btn_NextPage','第二页');click('CampShade/Picker/Btn_NextPage','第三页');click('CampShade/Picker/Btn_Select_46','等级2狼人鹰')
click('CampShade/Camp/Btn_ChooseUnits','起点队伍配置');click('CampShade/UnitPicker/Btn_Unit_army_2_002','起点不带已招募药草师');click('CampShade/UnitPicker/Btn_Back','返回配置')
click('CampShade/Camp/Btn_ChooseCards','原卡构筑');click('CampShade/DeedPicker/Btn_ClearDeeds','清除旧原牌选择')
for id in cards[:2]:click('CampShade/DeedPicker/Btn_ChooseDeed_'+id,'选原牌 '+id)
click('CampShade/DeedPicker/Btn_DeedNext','第二页基础牌')
for id in cards[2:4]:click('CampShade/DeedPicker/Btn_ChooseDeed_'+id,'选原牌 '+id)
click('CampShade/DeedPicker/Btn_DeedNext','第三页基础牌');click('CampShade/DeedPicker/Btn_ChooseDeed_'+cards[4],'选原无情威压')
click('CampShade/DeedPicker/Btn_DeedKind_1','原高级行动');click('CampShade/DeedPicker/Btn_DeedNext','第二页高级行动')
for id in cards[5:]:click('CampShade/DeedPicker/Btn_ChooseDeed_'+id,'选原牌 '+id)
click('CampShade/DeedPicker/Btn_DoneDeeds','确认七张实际牌');click('CampShade/Camp/Btn_Journey','同一原玩家连续旅程')
click('Tactic_day_1/Btn_Tactic_day_1','原捷足先登');click('Btn_BeginTurn','实际第一回合',{'hand':7,'units.count':0,'journey.offerCount':3})
interaction();click('JourneyInteraction/Btn_BeginInteraction','尚未在村庄，合法拒绝',{'rejections':1,'journey.interacting':'False'});closeinteraction()
hand();play('basic_card_000',expected={'movement':2});closehand()
click('Btn_JourneyMap','实际地图');click('JourneyMap/Btn_Hex_plains','选择村庄');click('JourneyMap/Btn_Move','真实支付移动2进入村庄',{'movement':0,'journey.position':'0,1','journey.atVillage':'True'},True);click('JourneyMap/Btn_CloseMap','返回')
interaction();click('JourneyInteraction/Btn_BeginInteraction','正式交涉并冻结本次声誉修正',{'influence':0,'journey.interacting':'True'});closeinteraction()
hand();play('basic_card_012',expected={'influence':2,'fame':4});play('advanced_card_014',expected={'influence':5,'reputation':0});closehand()
interaction();click('JourneyInteraction/Btn_Offer_army_2_001','选择无村庄图标守护者魔像');click('JourneyInteraction/Btn_Recruit','地点不符拒绝，不扣费不移牌',{'rejections':2,'influence':5,'journey.offerCount':3,'units.count':0})
click('JourneyInteraction/Btn_Offer_army_2_006','选择原农民');click('JourneyInteraction/Btn_Recruit','原影响力4实际支付，英雄事迹声誉+1',{'influence':1,'journey.offerCount':2,'units.count':1,'reputation':1,'fame':4,'journey.freeSlots':0},True);closeinteraction()
click('Btn_EndTurn','实际结束交涉回合，不重建新招部队',{'hand':4,'journey.completedTurns':1,'units.count':1})
click('Btn_BeginTurn','下一回合同一农民');click('Btn_JourneyEncounter_4','原狼骑兵战斗');advance();advance();advance()
actual+=['w','w']
click('Btn_Roster','显示实际新招部队');click('Units/Btn_Unit_0','原农民原图');click('Inspector/Btn_UnitAbility_0','实际攻击2，原农民耗竭',{'melee':2,'units.ready':'False','ui.unitArtCount':1},True)
click('Btn_Roster','返回实际手牌');click('Hand/#1','选择保留到下回合的原狂怒');click('Inspector/Btn_Choice_attack','原普通攻击');click('Inspector/Btn_Play','实际原狂怒攻击2',{'melee':4})
actual.remove('basic_card_021')
click('Enemy_0/Btn_Target','选择原狼骑兵');click('Inspector/Btn_Resolve','消费实际攻击4击杀',{'kills':1,'fame':7,'wounds':2})
advance();click('Inspector/Btn_EndTurn','战斗结束仍是同一耗竭部队和两伤',{'journey.completedTurns':2,'units.ready':'False','hand':5,'wounds':2})
click('Btn_BeginTurn','留在村庄的第三回合');interaction();click('JourneyInteraction/Btn_BeginInteraction','新回合重新交涉',{'influence':0});closeinteraction()
hand();play('advanced_card_019',expected={'influence':5,'wounds':2});play('basic_card_008',expected={'influence':7});closehand()
interaction();click('JourneyInteraction/Btn_BuyHealing','原村庄购买治疗1',{'influence':4,'healing':1});click('JourneyInteraction/Btn_BuyHealing','再购买治疗1',{'influence':1,'healing':2});closeinteraction()
hand();click('JourneyHand/Btn_HealHero','第一张真实手牌伤归还供应',{'wounds':1,'healing':1});click('JourneyHand/Btn_HealHero','第二张真实伤归还供应',{'wounds':0,'healing':0,'hand':1},True)
actual=['basic_card_026']
play('basic_card_026','strong',{'influence':7,'reputation':0,'red':0,'hand':0})
click('JourneyHand/Btn_RecoveryUnit_0','选择同一耗竭农民');click('JourneyHand/Btn_ReadyWithInfluence','按原等级1支付影响力2重整',{'influence':5,'units.ready':'True','units.wounds':'0'},True);closehand()
interaction();click('JourneyInteraction/Btn_EndInteraction','结束交涉清掉剩余影响力',{'influence':0,'journey.interacting':'False'});click('JourneyInteraction/Btn_BeginInteraction','同回合不再开始新交涉',{'rejections':3});closeinteraction()
click('Btn_EndTurn','原牌实际打出后结束第三回合',{'journey.completedTurns':3,'units.count':1,'hand':0,'hero.deckTotal':7})
click('Btn_Announce','合法声明结束');click('Btn_EndRound','同一牌库和队伍进入新轮');click('Btn_PrepareRound','实际部队供应仅新轮换整列',{'journey.offerCount':3,'units.count':1,'units.ready':'True','hero.deckTotal':7,'wounds':0},True)
if '--from' in sys.argv:
    boundary=int(sys.argv[sys.argv.index('--from')+1])
    for step in steps[:boundary]:step['after']=[];step['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=240,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
