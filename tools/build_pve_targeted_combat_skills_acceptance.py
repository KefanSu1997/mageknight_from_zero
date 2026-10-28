import json,sys
from pathlib import Path
mode=sys.argv[1];steps=[]
def click(path,label,expected=None,capture=False):
 steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='原技能真实敌实例、限定贡献、标记归属与正式连续回合') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def play(card,choice):
 click('Hand/Btn_Card_basic_card_'+card+'_0','原战斗牌');click('Inspector/Btn_Choice_'+choice,'原选择');click('Inspector/Btn_Play','原卡消费')
def skill():
 click('Btn_Skills','原技能');click('Skills/Btn_Skill_0','自有原技能')
def target(i=0):click('Enemy_'+str(i)+'/Btn_Target','实际敌人')
def endturn():
 click('Inspector/Btn_Next','正式结束战斗');click('Inspector/Btn_EndTurn','正式结束回合');click('Btn_BeginTurn','同一玩家下一回合',{'journey.samePlayer':'True'})
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','原英雄合法构筑'));click('CampShade/Camp/Btn_Loadout','原起点')
for _ in range(2):click('CampShade/Picker/Btn_NextPage','下一页')
click('CampShade/Picker/Btn_Select_40','真实原资源')
click('CampShade/Camp/Btn_ChooseCards','原卡构筑');p='CampShade/DeedPicker/';click(p+'Btn_ClearDeeds','清空旧构筑')
for c in ['000','001','007','008']:click(p+'Btn_ChooseDeed_basic_card_'+c,'既有原卡')
click(p+'Btn_DeedNext','下一页')
for c in ['016','021','022']:click(p+'Btn_ChooseDeed_basic_card_'+c,'既有原卡')
click(p+'Btn_DoneDeeds','确认原牌');click('CampShade/Camp/Btn_SmallDeckHeroBuild','原英雄技能')
hero,idx={'fork':('Braevalar',5),'duel':('Wolfhawk',7),'puppet':('Krang',6)}[mode]
click('CampShade/HeroBuild/Btn_Hero_'+hero,'原英雄');click('CampShade/HeroBuild/Btn_Level_2','合法等级');click('CampShade/HeroBuild/Btn_Skill_'+str(idx),'自身技能');click('CampShade/HeroBuild/Btn_Begin','同玩家旅程',{'hero.id':hero,'hero.skills':1})
click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','正式回合')
click('Btn_JourneyHand','原手牌');click('JourneyHand/Btn_Hand_0','原行进');click('JourneyHand/Btn_OutsideChoice_base','移动2');click('JourneyHand/Btn_PlayOutside','原移动');click('JourneyHand/Btn_CloseHand','返回');click('Btn_JourneyMap','真实地图');click('JourneyMap/Btn_Hex_plains','村庄目标');click('JourneyMap/Btn_Move','实际消耗移动');click('JourneyMap/Btn_CloseMap','返回');click('Btn_EndTurn','移动回合结束');click('Btn_BeginTurn','第二回合')
click('Btn_JourneyEncounter_'+('2' if mode=='fork' else '0'),'真实原敌')
click('Inspector/Btn_Next','格挡阶段')
if mode=='duel':
 skill();target();click('Inspector/Btn_UseSkill','决斗绑定实际敌人',{'duel.target':'monster_032','block':1},True);click('Btn_Skills','原牌');play('016','strong');target();click('Inspector/Btn_Resolve','实际格挡消费')
click('Inspector/Btn_Next','承伤阶段');click('Inspector/Btn_Next','真实承伤与近战')
if mode=='fork':
 skill();click('Inspector/Btn_UseSkill','原技能目标面板',capture=True)
 for i in range(2):click('EnemyTokenSkill/Btn_Enemy_'+str(i),'不同实际标记')
 click('EnemyTokenSkill/Btn_Confirm','各自绑定冰火远程1',{'power.boundAttacks':'monster_032:ColdFire:1,monster_032:ColdFire:1'},True);click('Btn_Skills','原牌');play('021','attack');target();click('Inspector/Btn_Resolve','仅消费第一个敌人贡献',{'kills':1,'power.boundAttacks':'monster_032:ColdFire:1'},True);play('016','attack');target(1);click('Inspector/Btn_Resolve','另一实例贡献独立消费',{'kills':2,'power.boundAttacks':''},True)
elif mode=='duel':
 play('021','attack');target();click('Inspector/Btn_Resolve','决斗攻击与同敌额外名望',{'kills':1,'fame':6,'duel.unitAffected':'False'},True)
else:
 play('021','attack');play('016','attack');target();click('Inspector/Btn_Resolve','实际击败登记候选',{'kills':1,'puppet.candidates':'monster_032'},True);skill();click('Inspector/Btn_UseSkill','真实战果选择',capture=True);click('EnemyTokenSkill/Btn_Enemy_0','实际击败标记');click('EnemyTokenSkill/Btn_Confirm','保存实际标记',{'puppet.stored':'monster_032','puppet.candidates':''},True)
endturn()
if mode=='puppet':
 click('Btn_JourneyEncounter_4','下一战真实狼骑兵')
 for _ in range(3):click('Inspector/Btn_Next','正式阶段')
 skill();click('Inspector/Btn_UseSkill','持久库存选择');click('EnemyTokenSkill/Btn_Mode_1','原攻击用途');click('EnemyTokenSkill/Btn_Enemy_0','上回合保存原标记');click('EnemyTokenSkill/Btn_Confirm','消费原标记攻击2',{'puppet.stored':'','melee':2},True);click('Btn_Skills','真实余牌')
 for c in ['008','022']:
  click('Hand/Btn_Card_basic_card_'+c+'_0','实际普通原牌');click('Inspector/Btn_Sideways','实际横置攻击1')
 target();click('Inspector/Btn_Resolve','傀儡贡献真实后续击杀',{'kills':1},True);endturn()
if '--from' in sys.argv:
 for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(mode,len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
