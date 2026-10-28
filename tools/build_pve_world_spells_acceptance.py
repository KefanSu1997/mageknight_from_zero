"""Actual world path, original guards, persistent conquest and end-turn reward."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='原地道路径与费用、实际守军身份、飞翼目标停攻、击杀征服及正式回合奖励') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def play(index,choice='base'):
    click('JourneyHand/Btn_Hand_'+str(index),'实际原手牌');click('JourneyHand/Btn_OutsideChoice_'+choice,'原半面 '+choice);click('JourneyHand/Btn_PlayOutside','正式出牌')
def confirm(label,expected=None,capture=False):click('DeedInputs/Btn_ConfirmInputs',label,expected,capture)
p='CampShade/DeedPicker/'
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','原地点连续构筑'));click('CampShade/Camp/Btn_Loadout','种子0起点');click('CampShade/Picker/Btn_NextPage','第二页');click('CampShade/Picker/Btn_NextPage','第三页');click('CampShade/Picker/Btn_Select_39','实际红蓝各1')
click('CampShade/Camp/Btn_ChooseCards','选择七张原牌');click(p+'Btn_ClearDeeds','清空选择');click(p+'Btn_ChooseDeed_basic_card_000','保留原行军')
click(p+'Btn_DeedKind_2','原法术');click(p+'Btn_ChooseDeed_magic_004','地道');click(p+'Btn_ChooseDeed_magic_009','火球');click(p+'Btn_DeedNext','第二页');click(p+'Btn_ChooseDeed_magic_023','清风之翼')
click(p+'Btn_DeedKind_3','原神器');click(p+'Btn_ChooseDeed_items_004','红宝石戒指');click(p+'Btn_ChooseDeed_items_007','绿宝石戒指');click(p+'Btn_DeedNext','第二页');click(p+'Btn_ChooseDeed_items_015','黑暗护符');click(p+'Btn_DoneDeeds','七原实例');click('CampShade/Camp/Btn_Journey','实际世界');click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','真实回合',{'source.available':'White,Blue,White'})
click('Btn_JourneyTools','真实公共源');click('JourneyTools/Btn_Source_0','白骰');click('JourneyTools/Btn_UseSource','取得一白标',{'tokens.white':1});click('JourneyTools/Btn_CloseTools','回旅程');click('Btn_JourneyHand','原手牌')
click('JourneyHand/Btn_Hand_5','绿宝石戒指');click('JourneyHand/Btn_ArtifactNormal','原正常效果');click('JourneyHand/Btn_PlayOutside','绿晶与绿标分开',{'green':1,'tokens.green':1,'fame':1})
click('JourneyHand/Btn_Hand_5','黑暗护符')
for _ in range(5):click('JourneyHand/Btn_ArtifactColor_0','选择黑色')
click('JourneyHand/Btn_PlayOutside','原护符白昼许可',{'tokens.black':1,'mana.allowBlackAtDay':'True'})
click('JourneyHand/Btn_Hand_4','红宝石戒指');click('JourneyHand/Btn_ArtifactBurn','焚毁');click('JourneyHand/Btn_PlayOutside','真实移除原戒，本回合无尽红黑',{'mana.infinite':'Red,Black','hand':4})
play(1,'strong');click('DeedInputs/Btn_Path_mountain','路径第一格山脉');click('DeedInputs/Btn_Path_tower','终点实际法师塔',capture=True);confirm('原地道绿黑费用、真实守军进入同一战斗',{'tokens.green':0,'green':1,'world.tunnel':'True','world.ignoreSite':'True','journey.position':'-1,-1','reputation':-1},True)
click('Hand/Btn_Card_magic_023_0','黑夜之翼');click('Inspector/Btn_Choice_strong','原下半');click('Inspector/Btn_Play','实际敌人选择');click('DeedInputs/Btn_WingsEnemy_0','选择实际法师塔守军',capture=True);confirm('支付白黑；首目标不额外花移动',{'tokens.white':0,'enemies.skipped':'0','movement':0},True)
click('Inspector/Btn_Next','格挡');click('Inspector/Btn_Next','承伤');click('Inspector/Btn_Next','飞翼实际阻止毒攻击',{'wounds':0})
click('Hand/Btn_Card_magic_009_0','原火球');click('Inspector/Btn_Choice_base','原上半');click('Inspector/Btn_Play','正式费用与火焰攻击',capture=True);click('Enemy_0/Btn_Target','同一实际守军');click('Inspector/Btn_Resolve','原火球消费击杀实际守军',{'kills':1});click('Inspector/Btn_Next','战斗结束');click('Inspector/Btn_FinishSite','征服地点，弃置实际守军，待领奖励',{'world.conquered':'-1:-1','world.rewardPending':'True','world.discard':'monster_002'},True)
click('SiteReward/Btn_Reward_0','选择实际供应首张原法术',capture=True);click('Inspector/Btn_EndTurn','正式回合末取得奖励并补牌，持续地图保留',{'journey.completedTurns':1,'journey.completedBattles':1,'world.conquered':'-1:-1','world.tunnel':'False','hand':2,'mana.infinite':'','mana.allowBlackAtDay':'False'},True)
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
