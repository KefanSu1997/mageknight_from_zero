"""Remaining resource artifacts share one actual village hire and protected-unit battle."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原金币影响力实际招募、同部队附旗承伤、原金色转色与实际付款、真实跨回合牌区') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
picker='CampShade/DeedPicker/'
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','原神器连续构筑'));click('CampShade/Camp/Btn_Loadout','选择已有起点');click('CampShade/Picker/Btn_Select_0','红蓝各1的原起点')
click('CampShade/Camp/Btn_ChooseCards','选原牌');click(picker+'Btn_ClearDeeds','清除旧选择')
click(picker+'Btn_ChooseDeed_basic_card_000','原行进');click(picker+'Btn_DeedNext','下一页基础');click(picker+'Btn_ChooseDeed_basic_card_021','原狂怒')
click(picker+'Btn_DeedKind_3','原神器')
for id in ['items_002','items_007','items_010','items_011']:click(picker+'Btn_ChooseDeed_'+id,'选原神器 '+id)
click(picker+'Btn_DeedNext','下页原神器');click(picker+'Btn_ChooseDeed_items_014','原皓日护符');click(picker+'Btn_DoneDeeds','确认七张真实原牌')
click('CampShade/Camp/Btn_Journey','开始同一玩家旅程');click('Tactic_day_1/Btn_Tactic_day_1','原捷足先登');click('Btn_BeginTurn','第一回合')
def hand():click('Btn_JourneyHand','打开实际手牌')
def close():click('JourneyHand/Btn_CloseHand','返回当前回合')
hand();click('JourneyHand/Btn_Hand_0','原行进');click('JourneyHand/Btn_OutsideChoice_base','基础移动');click('JourneyHand/Btn_PlayOutside','移动2');close()
click('Btn_JourneyMap','地图');click('JourneyMap/Btn_Hex_plains','选村庄');click('JourneyMap/Btn_Move','支付移动2进入实际村庄');click('JourneyMap/Btn_CloseMap','返回')
click('Btn_JourneyInteraction','原村庄交涉');click('JourneyInteraction/Btn_BeginInteraction','正式交涉');click('JourneyInteraction/Btn_CloseInteraction','保留实际交涉')
hand();click('JourneyHand/Btn_Hand_3','原无尽金币包');click('JourneyHand/Btn_PlayOutside','原影响力4与名望2',{'influence':4,'fame':2})
click('JourneyHand/Btn_Hand_2','原翡翠戒指');click('JourneyHand/Btn_PlayOutside','绿标记与绿晶分离',{'tokens.green':1,'green':1,'fame':3});close()
click('Btn_JourneyInteraction','实际供应');click('JourneyInteraction/Btn_Offer_army_2_006','选择原农民');click('JourneyInteraction/Btn_Recruit','实际支付金币包影响力4招募同一原农民',{'influence':0,'units.count':1,'journey.offerCount':2},True);click('JourneyInteraction/Btn_CloseInteraction','保留新招部队')
hand();click('JourneyHand/Btn_Hand_1','原防护旌旗');click('JourneyHand/Btn_RecoveryUnit_0','选真实新招农民');click('JourneyHand/Btn_PlayOutside','实际附旗，原牌离开手牌并绑定部队',{'hand':3,'units.count':1},True);close()
click('Btn_EndTurn','同一带旗单位与三张原牌跨回合',{'journey.completedTurns':1,'hand':3,'green':1,'tokens.green':0})
click('Btn_BeginTurn','下一回合');hand();click('JourneyHand/Btn_Hand_1','原无尽宝石袋');click('JourneyHand/Btn_ArtifactBurn','原焚毁选择');click('JourneyHand/Btn_PlayOutside','焚毁得五颗原魔力，原牌永久移除',{'journey.removedCount':1,'tokens.red':1,'tokens.blue':1,'tokens.green':1,'tokens.white':1,'tokens.gold':1})
click('JourneyHand/Btn_Hand_1','原皓日护符');click('JourneyHand/Btn_PlayOutside','普通皓日护符再获得一颗金色',{'tokens.gold':2});close()
click('Btn_JourneyTools','原魔力操作');click('JourneyTools/Btn_SourceColor_Red','选金色的实际基础颜色');click('JourneyTools/Btn_UseGold','实际一金转红，来源守恒',{'tokens.gold':1,'tokens.red':2},True);click('JourneyTools/Btn_CloseTools','返回当前回合')
click('Btn_JourneyEncounter_0','新招同一带旗农民进入战斗',{'units.armor':'4','units.count':1})
click('Inspector/Btn_Next','格挡阶段');click('Inspector/Btn_Next','正式承伤分配');click('Btn_Roster','实际部队原图');click('Units/Btn_Unit_0','选择同一农民');click('Enemy_0/Btn_Target','选择真实攻击4');click('Inspector/Btn_AssignUnit','预选同单位承伤')
click('Inspector/Btn_Next','实际护甲3加旗1吸收4，部队真实创伤而英雄无伤',{'units.wounds':'1','wounds':0,'units.armor':'4','ui.unitArtCount':1},True)
click('Btn_Roster','回到实际手牌');click('Hand/Btn_Card_basic_card_021_0','原狂怒');click('Inspector/Btn_Choice_strong','原红色强化');click('Inspector/Btn_Play','真实红魔力支付原攻击4',{'tokens.red':1,'melee':4})
click('Enemy_0/Btn_Target','原哨卫');click('Inspector/Btn_Resolve','消费实际攻击4击败',{'kills':1,'fame':5});click('Inspector/Btn_Next','结束战斗')
click('Inspector/Btn_EndTurn','保留真实部队创伤和魔晶，临时魔力清空',{'journey.completedTurns':2,'units.count':1,'units.wounds':'1','wounds':0,'tokens.red':0,'tokens.gold':0,'green':1,'journey.removedCount':1,'journey.discardCount':5})
if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from')+1])]:step['after']=[];step['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=240,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
