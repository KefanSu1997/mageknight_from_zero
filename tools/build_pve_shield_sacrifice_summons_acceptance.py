"""Four original spells in one persistent village-to-battle journey; no fixture reset."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='魅惑实际交涉一次折扣；献祭支付红费弃真实材料得库存魔晶；号召消耗许可使用真实供应原能力；燃烧护盾成功格挡后火攻及真实击杀') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def play(index,choice,expected=None,capture=False):
    click('JourneyHand/Btn_Hand_'+str(index),'选择当前实际原手牌');click('JourneyHand/Btn_OutsideChoice_'+choice,'原选择 '+choice);click('JourneyHand/Btn_PlayOutside','实际出牌 '+choice,expected,capture)
def hand():click('Btn_JourneyHand','实际手牌')
def close():click('JourneyHand/Btn_CloseHand','返回本回合')
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','构筑'));click('CampShade/Camp/Btn_Loadout','既有资源');click('CampShade/Picker/Btn_Select_1','红1蓝2白1绿0')
click('CampShade/Camp/Btn_ChooseCards','七张原牌');p='CampShade/DeedPicker/';click(p+'Btn_ClearDeeds','清空起点')
for id in ['000','001']:click(p+'Btn_ChooseDeed_basic_card_'+id,'原基础 '+id)
click(p+'Btn_DeedKind_2','原法术')
for id in ['008','011']:click(p+'Btn_ChooseDeed_magic_'+id,'原法术 '+id)
click(p+'Btn_DeedNext','法术第二页')
for id in ['020','021']:click(p+'Btn_ChooseDeed_magic_'+id,'原法术 '+id)
click(p+'Btn_DeedKind_3','神器');click(p+'Btn_ChooseDeed_items_008','原公正之剑作为实际献祭材料')
click(p+'Btn_DoneDeeds','确认七原牌');click('CampShade/Camp/Btn_Journey','同一玩家旅程');click('Tactic_day_1/Btn_Tactic_day_1','原白昼战术');click('Btn_BeginTurn','正式第一回合')
hand();play(0,'base',{'movement':2});close();click('Btn_JourneyMap','实际地图');click('JourneyMap/Btn_Hex_plains','村庄');click('JourneyMap/Btn_Move','支付2移动到村庄',{'movement':0,'journey.position':'0,1'});click('JourneyMap/Btn_CloseMap','返回')
click('Btn_JourneyInteraction','实际供应');click('JourneyInteraction/Btn_BeginInteraction','正式开始村庄交涉');click('JourneyInteraction/Btn_InteractionHand','交涉中出牌')
play(4,'base_discount',{'influence':4,'white':0,'charm.discount':3},True);close();click('Btn_JourneyInteraction','回到同次交涉');click('JourneyInteraction/Btn_Offer_army_2_006','实际供应农民');click('JourneyInteraction/Btn_Recruit','原费用4减3，实际支付1',{'influence':3,'charm.discount':0,'units.count':1,'journey.offerCount':2,'journey.freeSlots':0},True)
click('JourneyInteraction/Btn_EndInteraction','结束交涉',{'influence':0});click('JourneyInteraction/Btn_CloseInteraction','返回');click('Btn_EndTurn','正式结束招募回合',{'journey.completedTurns':1,'journey.discardCount':2,'hand':5});click('Btn_BeginTurn','同一玩家第二回合')
hand();play(2,'base');click('DeedInputs/Btn_InputMaterial_0','真实宁静绿色材料');click('DeedInputs/Btn_InputMaterial_3','真实公正之剑神器材料');click('DeedInputs/Btn_SacrificeColor_0_White','为此神器独立选择白色',capture=True)
click('DeedInputs/Btn_ConfirmInputs','支付红晶1再得红晶1；宁静和剑弃牌得绿白晶',{'red':1,'green':1,'white':1,'tokens.red':0,'tokens.green':0,'tokens.white':0,'journey.discardCount':4,'hand':2},True)
play(1,'base');click('DeedInputs/Btn_ConfirmInputs','所得白晶实际支付战斗号召',{'white':0,'tokens.white':0,'offer.uses':1,'hand':1});close();click('Btn_JourneyEncounter_2','双重哨戒，两枚独立原标记');click('Inspector/Btn_Next','推进格挡')
click('Hand/Btn_Card_magic_008_0','原燃烧护盾');click('Inspector/Btn_Choice_base','基础火焰格挡');click('Inspector/Btn_Play','所得红晶支付原红费',{'red':0,'tokens.red':0,'block':4});click('Enemy_0/Btn_Target','第一枚原敌人');click('Inspector/Btn_Resolve','火焰格挡4挡住原攻击4',{'block':0,'melee':0},True)
click('Inspector/Btn_Next','伤害阶段');click('Inspector/Btn_Next','另一敌人真实承伤并取得护盾火攻',{'wounds':2,'melee':4});click('Enemy_0/Btn_Target','目标第一枚敌人');click('Inspector/Btn_Resolve','护盾火焰攻击4击杀原护甲3',{'kills':1,'fame':2,'melee':0},True)
click('Btn_OfferedUnits','消费原战斗号召许可');click('OfferedUnits/Btn_Offer_1','实际供应北地僧侣');click('OfferedUnits/Btn_Ability_0','原免费攻击或格挡3');click('OfferedUnits/Btn_Option_0','实际近战攻击3',capture=True);click('OfferedUnits/Btn_Use','使用后供应不入队不耗竭',{'offer.uses':0,'melee':3,'units.count':1,'offer.ids':'army_2_001,army_2_005'},True)
click('Enemy_1/Btn_Target','第二枚仍存活敌人');click('Inspector/Btn_Resolve','供应攻击3实际击杀',{'kills':2,'fame':4,'melee':0});click('Inspector/Btn_Next','真实战斗结束');click('Inspector/Btn_EndTurn','正式结束第二回合保留伤牌、农民及七张原牌去向',{'journey.completedTurns':2,'journey.completedBattles':1,'wounds':2,'hand':2,'journey.discardCount':7,'tokens.green':0,'green':1,'units.count':1,'units.ready':'True','units.wounds':'0','journey.samePlayer':'True'},True)
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
