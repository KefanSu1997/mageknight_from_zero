"""Three formal Braevalar turns: paid move->block, ice block->attack, real material attack->move."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='变换身形选择真实原基本牌固定项，保留原费用材料和元素，同一玩家正式回合连续消费') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def play(card,choice):
    click('Hand/Btn_Card_basic_card_'+card+'_0','原牌');click('Inspector/Btn_Choice_'+choice,'印刷原效果');click('Inspector/Btn_Play','实际出牌')
def skill(card,option,strong=False):
    click('Btn_Skills','原技能');click('Skills/Btn_Skill_0','变换身形');click('Inspector/Btn_UseSkill','原基本牌选择');click('Shapeshift/Btn_Card_'+str(card),'实际基本牌')
    if strong:click('Shapeshift/Btn_Strong','原强化付款')
    click('Shapeshift/Btn_Output_'+str(option),'选择原固定项',capture=True)
def finish(turn):
    click('Inspector/Btn_Next','正式战斗结束');click('Inspector/Btn_EndTurn','同玩家正式结束回合',{'journey.completedTurns':turn},True)
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','原英雄构筑'));click('CampShade/Camp/Btn_Loadout','原卡起点')
for _ in range(2):click('CampShade/Picker/Btn_NextPage','下一页')
click('CampShade/Picker/Btn_Select_39','原蓝红库存')
p='CampShade/DeedPicker/'
click('CampShade/Camp/Btn_ChooseCards','七张实际原卡');click(p+'Btn_ClearDeeds','清空旧构筑')
for c in ['000','007']:click(p+'Btn_ChooseDeed_basic_card_'+c,'选择原基础牌')
click(p+'Btn_DeedNext','下一页')
for c in ['014','016','020','021','023']:click(p+'Btn_ChooseDeed_basic_card_'+c,'选择原基础牌')
click(p+'Btn_DoneDeeds','确认');click('CampShade/Camp/Btn_SmallDeckHeroBuild','本英雄原技能');click('CampShade/HeroBuild/Btn_Hero_Braevalar','布拉沃');click('CampShade/HeroBuild/Btn_Level_4','合法等级4');click('CampShade/HeroBuild/Btn_Skill_6','原变换身形');click('CampShade/HeroBuild/Btn_Begin','同一真实玩家',{'hero.id':'Braevalar','hero.skills':1})
click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','正式第一回合');click('Btn_JourneyEncounter_0','实际兽人');click('Inspector/Btn_Next','实际格挡阶段')
skill(2,0,True);click('Shapeshift/Btn_Confirm','蓝费原移动4转格挡4',{'power.blocks':'Physical:4','blue':0,'movement':0,'skills.used':'True'},True)
click('Btn_Skills','返回原手牌');click('Enemy_0/Btn_Target','实际攻击目标');click('Inspector/Btn_Resolve','真实格挡消费',{'block':0},True)
click('Inspector/Btn_Next','正式承伤');click('Inspector/Btn_Next','正式近战',{'wounds':0})
play('016','attack');play('021','attack');click('Enemy_0/Btn_Target','实际敌人');click('Inspector/Btn_Resolve','原攻击4实际击杀',{'kills':1},True);finish(1)
click('Btn_BeginTurn','同玩家第二回合');click('Btn_JourneyEncounter_4','实际狼骑兵')
for _ in range(3):click('Inspector/Btn_Next','正式战斗阶段')
skill(2,1);click('Shapeshift/Btn_Confirm','原冰格挡3转冰攻击3',{'power.attacks':'Melee:Ice:3','skills.used':'True'},True)
click('Btn_Skills','返回原牌');click('Hand/Btn_Card_basic_card_007_0','实际敏捷');click('Inspector/Btn_Sideways','原横置攻击1');click('Enemy_0/Btn_Target','真实狼骑兵');click('Inspector/Btn_Resolve','冰3加物理1实际击杀',{'kills':1},True);finish(2)
click('Btn_BeginTurn','同玩家第三回合');click('Btn_JourneyHand','保留原材料');click('JourneyHand/Btn_Support','原技能');click('JourneySupport/Btn_OutsideSkill_0','原变换身形');click('JourneySupport/Btn_UseOutsideSkill','选择基本牌');click('Shapeshift/Btn_Card_1','真实随机应变');click('Shapeshift/Btn_Output_2','原攻击3');click('Shapeshift/Btn_Material','选择实际弃牌');click('Shapeshift/Btn_Card_0','原行进材料',capture=True);click('Shapeshift/Btn_Confirm','实际弃材料且固定攻击转移动3',{'movement':3},True)
click('JourneySupport/Btn_BackSupport','返回');click('JourneyHand/Btn_CloseHand','地图');click('Btn_JourneyMap','实际地形');click('JourneyMap/Btn_Hex_forest','实际森林');click('JourneyMap/Btn_Move','原森林费用3实际消费',{'movement':0,'journey.position':'1,0'},True);click('JourneyMap/Btn_CloseMap','继续同玩家');click('Btn_EndTurn','正式三回合守恒',{'journey.completedTurns':3,'journey.completedBattles':2},True)
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
