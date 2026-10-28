"""Shared real supplies, material identities and subsequent consumers in one journey."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原卡材料与同色实际供应、牌区去向、印刷效果与实际后续消费') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
p='CampShade/DeedPicker/'
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','连续旅程材料神器构筑'));click('CampShade/Camp/Btn_Loadout','选既有显式种子6重放起点')
click('CampShade/Picker/Btn_NextPage','配置第二页');click('CampShade/Picker/Btn_NextPage','配置第三页');click('CampShade/Picker/Btn_Select_40','原黑色号角配置的显式种子6，改选原手牌')
click('CampShade/Camp/Btn_ChooseCards','原卡目录');click(p+'Btn_ClearDeeds','清除旧选择')
for id in ['basic_card_000','basic_card_007']:click(p+'Btn_ChooseDeed_'+id,'选原材料 '+id)
click(p+'Btn_DeedNext','基础第二页')
for id in ['basic_card_014','basic_card_021']:click(p+'Btn_ChooseDeed_'+id,'选原材料 '+id)
click(p+'Btn_DeedKind_3','原神器');click(p+'Btn_DeedNext','神器第二页')
for id in ['items_013','items_021','items_023']:click(p+'Btn_ChooseDeed_'+id,'选原神器 '+id)
click(p+'Btn_DoneDeeds','确认七张实际原牌');click('CampShade/Camp/Btn_Journey','同一玩家连续旅程');click('Tactic_day_1/Btn_Tactic_day_1','原白昼战术');click('Btn_BeginTurn','正式回合',{'supply.advanced':'advanced_card_039,advanced_card_023,advanced_card_004','supply.spells':'magic_002,magic_010,magic_017','supply.artifacts':22})
click('Btn_JourneyHand','实际手牌');click('JourneyHand/Btn_Hand_4','智慧之书原图');click('JourneyHand/Btn_PlayOutside','展开原材料和真实供应')
click('MaterialArtifact/Btn_Material_0','选实际绿色行进材料',{'material.selected':'basic_card_000'})
click('MaterialArtifact/Btn_ArtifactOffer_0','选择同绿山川逸闻原供应',{'material.offer':'advanced_card_039'},True)
click('MaterialArtifact/Btn_ConfirmMaterial','永久去除行进，取得同色原高级行动',{'hand':6,'journey.removedCount':1,'removed.ids':'basic_card_000','played':1})
# Current hand: Agility, Stamina, Rage, Staff, Tome, acquired Mountain Lore.
click('JourneyHand/Btn_Hand_3','德鲁伊法杖');click('JourneyHand/Btn_PlayOutside','展开原法杖选择')
click('MaterialArtifact/Btn_Material_1','实际蓝色耐力作为弃牌材料');click('MaterialArtifact/Btn_StaffFirst_1','蓝色原效果');click('MaterialArtifact/Btn_StaffCrystal_White','两颗晶体选白色',None,True)
click('MaterialArtifact/Btn_ConfirmMaterial','实际弃耐力，白晶进入库存',{'hand':4,'white':2,'journey.discardCount':1,'discard.ids':'basic_card_014','played':2})
click('JourneyHand/Btn_Hand_3','刚取得的同一山川逸闻');click('JourneyHand/Btn_OutsideChoice_base','原基础移动3');click('JourneyHand/Btn_PlayOutside','新取得原牌实际生效',{'movement':3});click('JourneyHand/Btn_CloseHand','返回真实地图')
click('Btn_JourneyMap','实际地图');click('JourneyMap/Btn_Hex_plains','选择相邻村庄');click('JourneyMap/Btn_Move','实际支付移动2',{'journey.position':'0,1','movement':1},True);click('JourneyMap/Btn_CloseMap','保留移动后玩家')
click('Btn_JourneyEncounter_2','同一原牌资源进入双敌战斗');click('Hand/Btn_Card_basic_card_007_0','原敏捷');click('Inspector/Btn_Choice_strong','原白色强化');click('Inspector/Btn_Play','支付法杖所给白晶',{'white':1,'ranged':3})
click('Enemy_0/Btn_Target','选择第一原哨卫');click('Inspector/Btn_Resolve','实际消费远程3击败',{'kills':1,'fame':2})
click('Inspector/Btn_Next','格挡');click('Inspector/Btn_Next','承伤');click('Inspector/Btn_Next','第二敌实际造成两张伤牌',{'wounds':2})
click('Hand/Btn_Card_items_023_0','原法术宝典');click('Inspector/Btn_OpenMaterialArtifact','展开当前真实法术供应');click('MaterialArtifact/Btn_Burn','焚毁借原下半')
click('MaterialArtifact/Btn_Material_0','原红色狂怒作为弃牌材料');click('MaterialArtifact/Btn_ArtifactOffer_1','原烈焰之墙供应');click('MaterialArtifact/Btn_TomeChoice_strong_attack','原烈焰浪涌攻击分支',None,True)
click('MaterialArtifact/Btn_ConfirmMaterial','实际弃红牌、焚毁宝典，原火攻5加交战2乘2',{'melee':9,'journey.removedCount':2,'removed.ids':'basic_card_000,items_023','red':1,'tokens.black':0,'supply.spells':'magic_002,magic_010,magic_017'})
click('Enemy_1/Btn_Target','选择第二原哨卫');click('Inspector/Btn_Resolve','原火攻实际击败',{'kills':2,'fame':4},True)
click('Inspector/Btn_Next','结束同一场战斗');click('Inspector/Btn_EndTurn','正式回合保留牌区与伤牌，供应不被借走',{'journey.completedTurns':1,'journey.completedBattles':1,'wounds':2,'white':1,'journey.discardCount':6,'journey.removedCount':2,'supply.spells':'magic_002,magic_010,magic_017'})
if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from')+1])]:step['after']=[];step['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=240,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
