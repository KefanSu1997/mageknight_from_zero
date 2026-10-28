import json,sys
from pathlib import Path
steps=[]
def click(path,label,expect=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='原忠诚契约实际供应/专属槽/不可解散；原地下城夜间/禁部队/胜败弃敌及真实奖励') for k,v in {'ui.truncated':'',**(expect or {})}.items()]))
def hand(i,choice='base',expect=None):
    click('Btn_JourneyHand','真实手牌');click('JourneyHand/Btn_Hand_'+str(i),'选择原牌');click('JourneyHand/Btn_OutsideChoice_'+choice,'原选择');click('JourneyHand/Btn_PlayOutside','原牌实际消费',expect);click('JourneyHand/Btn_CloseHand','返回旅程')
def play(card,choice):
    click('Hand/Btn_Card_basic_card_'+card+'_0','原战斗牌');click('Inspector/Btn_Choice_'+choice,'原效果');click('Inspector/Btn_Play','真实原卡出牌')
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','原英雄合法构筑'));click('CampShade/Camp/Btn_Loadout','可重放原起点')
for _ in range(2):click('CampShade/Picker/Btn_NextPage','下一页')
click('CampShade/Picker/Btn_Select_40','原资源与种子')
click('CampShade/Camp/Btn_ChooseCards','原卡构筑');p='CampShade/DeedPicker/';click(p+'Btn_ClearDeeds','清空旧构筑')
for c in ['000','001','007','008']:click(p+'Btn_ChooseDeed_basic_card_'+c,'既有原卡')
click(p+'Btn_DeedNext','下一页')
for c in ['016','021','022']:click(p+'Btn_ChooseDeed_basic_card_'+c,'既有原卡')
click(p+'Btn_DoneDeeds','确认原牌');click('CampShade/Camp/Btn_ChooseUnits','普通已招部队');click('CampShade/UnitPicker/Btn_Unit_army_2_002','普通药草师');click('CampShade/UnitPicker/Btn_Back','返回配置')
click('CampShade/Camp/Btn_SmallDeckHeroBuild','原英雄技能');click('CampShade/HeroBuild/Btn_Hero_Norowas','诺若威斯');click('CampShade/HeroBuild/Btn_Level_2','合法等级2');click('CampShade/HeroBuild/Btn_Skill_7','忠诚契约');click('CampShade/HeroBuild/Btn_Begin','真实供给后取得契约',{'hero.id':'Norowas','hero.skills':1,'journey.offerCount':5,'loyalty.available':'True','command.ordinaryFree':0},True)
click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','正式第一回合');hand(0)
click('Btn_JourneyMap','真实地图');click('JourneyMap/Btn_Hex_plains','当前村庄目标');click('JourneyMap/Btn_Move','实际移动2',{'journey.position':'0,1','movement':0});click('JourneyMap/Btn_CloseMap','返回')
click('Btn_JourneyInteraction','实际供应');click('JourneyInteraction/Btn_NextOffer','新增真实常规供应第二页',capture=True);click('JourneyInteraction/Btn_NextOffer','返回第一页');click('JourneyInteraction/Btn_BeginInteraction','原村庄交涉');click('JourneyInteraction/Btn_Offer_army_2_006','原农民');click('JourneyInteraction/Btn_LoyaltySlot','明确选择忠诚专属槽',{'loyalty.selected':'True'},True);click('JourneyInteraction/Btn_Recruit','原费4减5至0真实招募',{'units.count':2,'influence':0,'loyalty.bound':'army_2_006','loyalty.available':'False','command.ordinaryFree':0},True)
click('JourneyInteraction/Btn_Owned_1','同一契约单位');click('JourneyInteraction/Btn_Dismiss','原契约拒绝主动解散',{'rejections':1,'units.count':2,'loyalty.bound':'army_2_006'},True);click('JourneyInteraction/Btn_CloseInteraction','保留实际部队');click('Btn_EndTurn','第一回合结束');click('Btn_BeginTurn','同玩家第二回合');hand(1)
click('Btn_JourneyMap','实际地图');click('JourneyMap/Btn_Hex_dungeon','真实地下城格');click('JourneyMap/Btn_Move','实际进入地下城位置',{'journey.position':'1,1','movement':0});click('JourneyMap/Btn_Dungeon','原地点规则',capture=True);click('DungeonIntro/Btn_EnterDungeon','抽实际棕敌并进入夜战',{'combat.dayPart':'Night','combat.unitsAllowed':'False','units.participating':'False,True'},True)
click('Inspector/Btn_Next','原格挡阶段');click('Btn_Roster','实际部队');click('Units/Btn_Unit_0','普通部队被地点禁用',capture=True);click('Units/Btn_Unit_1','同一忠诚农民');click('Inspector/Btn_UnitAbility_0','实际部队格挡2',{'block':2,'units.ready':'True,False'},True);click('Enemy_0/Btn_Target','九头蛇实际攻击一');click('Inspector/Btn_Resolve','实际格挡攻击一')
click('Inspector/Btn_Next','真实承伤阶段');click('Units/Btn_Unit_1','耗竭契约部队仍可承伤');click('Enemy_0/Btn_Target','实际九头蛇');click('Inspector/Btn_AttackSlot','实际第二攻击槽');click('Inspector/Btn_AssignUnit','契约单位加入实际承伤');click('Inspector/Btn_Next','实际承伤结算',{'units.wounds':'0,1','wounds':1},True)
click('Btn_Roster','返回原牌');play('021','strong');play('016','attack');click('Enemy_0/Btn_Target','真实九头蛇');click('Inspector/Btn_Resolve','原攻击6击败九头蛇',{'kills':1},True);click('Inspector/Btn_Next','真实地点战后结算',{'combat.dayPart':'Day','combat.unitsAllowed':'True','world.discard':'monster_066','dungeon.reward':'Spell','dungeon.completed':'True'},True)
click('Inspector/Btn_SiteReward','原法术奖励供应',capture=True);click('SiteReward/Btn_Reward_0','选择实际原法术');click('Inspector/Btn_EndTurn','原回合末领取并补牌',{'journey.completedTurns':2,'journey.completedBattles':1,'units.wounds':'0,1','loyalty.bound':'army_2_006'},True);click('Btn_BeginTurn','同玩家第三回合',{'journey.samePlayer':'True','loyalty.bound':'army_2_006'},True)
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
