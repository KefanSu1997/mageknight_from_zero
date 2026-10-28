"""Legal hero routes for original map skills; custom terrain does not certify original tile exploration."""
import json,sys
from pathlib import Path
mode=sys.argv[1];steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='原英雄技能的实际费用、次数、地点实例与正式连续回合；自定地形不替代原七格探索验收') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def outside_skill(index,option=None,expected=None):
    click('Btn_JourneyHand','实际手牌');click('JourneyHand/Btn_Support','原技能');click('JourneySupport/Btn_OutsideSkill_'+str(index),'自有原技能',capture=True)
    if option is not None:click('JourneySupport/Btn_OutsideOption_'+str(option),'原印刷选择')
    click('JourneySupport/Btn_UseOutsideSkill','正式技能入口',expected,True)
def close_support():
    click('JourneySupport/Btn_BackSupport','返回手牌');click('JourneyHand/Btn_CloseHand','返回连续旅程')
def outside_card(index,choice='base'):
    click('Btn_JourneyHand','实际原牌');click('JourneyHand/Btn_Hand_'+str(index),'选择实际手牌');click('JourneyHand/Btn_OutsideChoice_'+choice,'原移动效果');click('JourneyHand/Btn_PlayOutside','实际出牌');click('JourneyHand/Btn_CloseHand','返回')
def travel(site,expected):
    click('Btn_JourneyMap','实际地图');click('JourneyMap/Btn_Hex_'+site,'实际目的地',capture=True);click('JourneyMap/Btn_Move','实际移动与费用消费',expected,True);click('JourneyMap/Btn_CloseMap','继续同玩家')
def play(card,choice):
    click('Hand/Btn_Card_basic_card_'+card+'_0','原手牌');click('Inspector/Btn_Choice_'+choice,'原效果');click('Inspector/Btn_Play','实际出牌')
def wild(option):
    click('Btn_Skills','原技能');click('Skills/Btn_Skill_0','野性盟友');click('Inspector/Btn_SkillChoice_'+str(option),'原攻击或减攻')
    if option==1:click('Enemy_0/Btn_Target','实际敌人攻击槽')
    click('Inspector/Btn_UseSkill','正式原技能消费',{'enemies.attacks':'3'} if option else {'melee':1},True);click('Btn_Skills','返回原牌')
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','合法原构筑'));click('CampShade/Camp/Btn_Loadout','原卡与资源')
for _ in range(2):click('CampShade/Picker/Btn_NextPage','下一页')
click('CampShade/Picker/Btn_Select_39','红蓝各一晶')
click('CampShade/Camp/Btn_ChooseCards','实际原牌');p='CampShade/DeedPicker/';click(p+'Btn_ClearDeeds','清空旧构筑')
for c in ['000','001','007']:click(p+'Btn_ChooseDeed_basic_card_'+c,'原基础牌')
click(p+'Btn_DeedNext','下一页')
for c in ['014','016','021','022']:click(p+'Btn_ChooseDeed_basic_card_'+c,'原基础牌')
click(p+'Btn_DoneDeeds','确认');click('CampShade/Camp/Btn_SmallDeckHeroBuild','合法原英雄')
hero,skills={'wild':('Braevalar',[1,7]),'eagle':('Wolfhawk',[2]),'flight':('Goldyx',[6])}[mode]
click('CampShade/HeroBuild/Btn_Hero_'+hero,'正确英雄归属');click('CampShade/HeroBuild/Btn_Level_4','合法等级4')
for s in skills:click('CampShade/HeroBuild/Btn_Skill_'+str(s),'原技能')
click('CampShade/HeroBuild/Btn_Begin','同一真实玩家',{'hero.id':hero,'hero.skills':len(skills)})
click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','正式回合开始')
if mode=='wild':
    outside_skill(1,1,{'blue':0,'movement':1,'world.explorationCost':1});close_support()
    outside_card(2);travel('lake',{'movement':1,'journey.position':'-1,0'})
    click('Btn_JourneyEncounter_0','实际兽人');wild(1)
    for _ in range(3):click('Inspector/Btn_Next','正式阶段')
    play('021','strong');click('Enemy_0/Btn_Target','实际兽人');click('Inspector/Btn_Resolve','红费强化实际击杀',{'kills':1,'wounds':1,'red':0},True)
    click('Inspector/Btn_Next','战斗结束');click('Inspector/Btn_EndTurn','正式回合结束',{'journey.completedTurns':1},True);click('Btn_BeginTurn','同玩家第二回合')
    outside_skill(1,0,{'blue':0,'movement':1});close_support();outside_card(0);outside_card(1)
    travel('mountain',{'movement':0,'journey.position':'0,-1'})
    click('Btn_JourneyEncounter_4','实际狼骑兵')
    for _ in range(3):click('Inspector/Btn_Next','正式战斗阶段')
    wild(0);play('016','attack');click('Hand/Btn_Card_basic_card_001_0','实际宁静');click('Inspector/Btn_Sideways','原横置攻击1');click('Enemy_0/Btn_Target','实际狼骑兵');click('Inspector/Btn_Resolve','技能与原牌实际击杀',{'kills':1,'melee':0},True)
    click('Inspector/Btn_Next','战斗结束');click('Inspector/Btn_EndTurn','同玩家两回合',{'journey.completedTurns':2,'journey.completedBattles':2},True)
elif mode=='eagle':
    click('Btn_JourneyMap','原地图可见性');click('JourneyMap/Btn_Hex_tower','两格实际法师塔',{'world.mapTargetRevealed':'True'},True)
    click('JourneyMap/Btn_MapGuards','查看同一实际公开守军',capture=True);click('MapGuards/Btn_CloseGuards','返回');click('JourneyMap/Btn_CloseMap','未主动使用')
    outside_skill(0,0,{'movement':1,'journey.outsideSkillUsed':'True'});close_support();outside_card(0)
    travel('forest',{'movement':0,'journey.position':'1,0'});click('Btn_EndTurn','正式下一回合',{'journey.completedTurns':1},True);click('Btn_BeginTurn','原次数恢复')
    outside_skill(0,0,{'movement':1,'journey.outsideSkillUsed':'True'});close_support();outside_card(1);travel('camp',{'movement':1,'journey.position':'0,0'});click('Btn_EndTurn','持续真实位置与牌区',{'journey.completedTurns':2},True)
else:
    outside_card(0);travel('plains',{'movement':0,'journey.position':'0,1'});outside_card(1)
    outside_skill(0);click('JourneyMap/Btn_Hex_desert','两格安全落点',capture=True);click('JourneyMap/Btn_Move','实际飞行消费2移动',{'movement':0,'journey.position':'1,-1','journey.outsideSkillUsed':'True'},True);click('JourneyMap/Btn_CloseMap','返回正式回合');click('Btn_EndTurn','保留本轮次数',{'journey.completedTurns':1},True)
    click('Btn_BeginTurn','同轮下一正式回合');click('Btn_JourneyHand','保留真实手牌');click('JourneyHand/Btn_Support','原飞行次数');click('JourneySupport/Btn_OutsideSkill_0','本轮已使用',{'journey.outsideSkillUsed':'True'},True);close_support();outside_card(1);travel('camp',{'movement':0,'journey.position':'0,0'});click('Btn_EndTurn','实际普通移动后结束',{'journey.completedTurns':2},True)
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(mode,len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
