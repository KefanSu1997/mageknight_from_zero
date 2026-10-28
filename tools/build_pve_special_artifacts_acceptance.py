"""Real command-banner recruit persists into a battle with a borrowed box artifact."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='指挥旗实际招募与指挥标记、宝盒物理身份、真实激活奖励和原供应归还') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
p='CampShade/DeedPicker/'
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','原特殊神器连续构筑'));click('CampShade/Camp/Btn_Loadout','既有显式种子0起点');click('CampShade/Picker/Btn_NextPage','第二页');click('CampShade/Picker/Btn_NextPage','第三页');click('CampShade/Picker/Btn_Select_39','种子0重放')
click('CampShade/Camp/Btn_ChooseCards','选原卡');click(p+'Btn_ClearDeeds','清除旧选择')
for id in ['basic_card_000','basic_card_007']:click(p+'Btn_ChooseDeed_'+id,'原牌 '+id)
click(p+'Btn_DeedNext','基础第二页')
for id in ['basic_card_014','basic_card_021']:click(p+'Btn_ChooseDeed_'+id,'原牌 '+id)
click(p+'Btn_DeedKind_3','原神器');click(p+'Btn_DeedNext','原神器第二页')
for id in ['items_017','items_022']:click(p+'Btn_ChooseDeed_'+id,'原神器 '+id)
click(p+'Btn_DeedNext','原神器第三页');click(p+'Btn_ChooseDeed_items_024','原神秘宝盒');click(p+'Btn_DoneDeeds','确认七张原牌')
click('CampShade/Camp/Btn_Journey','同一玩家与供应');click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','首回合')
click('Btn_JourneyHand','实际手牌');click('JourneyHand/Btn_Hand_0','原行进');click('JourneyHand/Btn_PlayOutside','原移动2');click('JourneyHand/Btn_CloseHand','返回')
click('Btn_JourneyMap','实际地图');click('JourneyMap/Btn_Hex_plains','村庄');click('JourneyMap/Btn_Move','实际付费进入');click('JourneyMap/Btn_CloseMap','返回村庄')
click('Btn_JourneyHand','战外原手牌');click('JourneyHand/Btn_Hand_4','原启迪头环');click('JourneyHand/Btn_PlayOutside','检查实际公共技能区，不伪造供应',{'supply.skills':''},True);click('SpecialArtifact/Btn_CloseSpecial','保留头环等待真实公共技能')
click('JourneyHand/Btn_Hand_3','原指挥旌旗');click('JourneyHand/Btn_PlayOutside','展开原指挥效果');click('SpecialArtifact/Btn_ConfirmSpecial','普通影响力4，尚无额外通用指挥槽',{'influence':4,'command.banners':0,'command.free':1});click('JourneyHand/Btn_CloseHand','返回')
click('Btn_JourneyInteraction','实际村庄交涉');click('JourneyInteraction/Btn_BeginInteraction','正式交涉');click('JourneyInteraction/Btn_CommandRecruit_0','明确选择旗代替新单位指挥标记',{'command.selected':'items_017'},True)
click('JourneyInteraction/Btn_Offer_army_2_006','原农民供应');click('JourneyInteraction/Btn_Recruit','真实支付招募并绑定指挥旗',{'influence':0,'command.banners':1,'command.free':1,'units.bannerIds':'items_017','units.count':1},True);click('JourneyInteraction/Btn_CloseInteraction','保留真实部队')
click('Btn_EndTurn','同一指挥旗农民跨回合',{'journey.completedTurns':1,'hand':5});click('Btn_BeginTurn','下一回合');click('Btn_JourneyHand','实际原手牌');click('JourneyHand/Btn_Hand_4','原宝盒');click('JourneyHand/Btn_PlayOutside','原宝盒选择')
click('SpecialArtifact/Btn_ConfirmSpecial','从实际牌堆揭示原荣誉旗',{'box.revealed':'items_000','box.used':'False','supply.artifacts':21},True)
click('SpecialArtifact/Btn_SpecialBurn','选择借焚毁，保留已附指挥旗');click('SpecialArtifact/Btn_ConfirmSpecial','实际焚毁宝盒本体并获得原使用名望',{'box.used':'True','fame':1,'removed.ids':'items_024','units.bannerIds':'items_017'});click('JourneyHand/Btn_CloseHand','保留实际资源进入后续')
click('Btn_JourneyEncounter_0','同一农民进入原哨卫战斗');click('Inspector/Btn_Next','格挡');click('Inspector/Btn_Next','分配');click('Inspector/Btn_Next','英雄实际两伤，保留部队用于攻击',{'wounds':2,'units.wounds':'0'})
click('Btn_Roster','实际部队');click('Units/Btn_Unit_0','同一带指挥旗农民');click('Inspector/Btn_UnitAbility_0','原农民攻击2加荣誉1，并得一次名望',{'melee':3,'fame':2,'units.ready':'False'},True)
click('Btn_Roster','原手牌');click('Hand/Btn_Card_basic_card_021_0','原狂怒');click('Inspector/Btn_Choice_attack','基础攻击');click('Inspector/Btn_Play','原狂怒2与农民荣誉攻击3合并为5',{'melee':5});click('Enemy_0/Btn_Target','原敌人');click('Inspector/Btn_Resolve','实际击杀与名望',{'kills':1,'fame':4});click('Inspector/Btn_Next','结束战斗')
click('Inspector/Btn_EndTurn','原神器归底，宝盒永久移除，原指挥旗与同部队保留',{'journey.completedTurns':2,'supply.artifacts':22,'box.revealed':'','removed.ids':'items_024','units.bannerIds':'items_017','command.banners':1,'wounds':2,'journey.discardCount':2},True)
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=240,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
