"""Three legal original hero routes, typed contributions and actual continuous consumers."""
import json,sys
from pathlib import Path
hero=sys.argv[1] if len(sys.argv)>1 else 'Krang'
soul=hero=='Krang';bull=hero=='Wolfhawk';leader=hero=='Norowas'
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='原技能加值绑定实际牌或部队贡献，原元素与费用保留；正式回合保持实体，结算真实消费') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def hand():click('Btn_JourneyHand','真实手牌')
def closehand():click('JourneyHand/Btn_CloseHand','返回旅程')
def support():click('JourneyHand/Btn_Support','原自身技能')
def closesupport():click('JourneySupport/Btn_BackSupport','返回手牌')
def move(site,remaining,pos):
    click('Btn_JourneyMap','真实地图');click('JourneyMap/Btn_Hex_'+site,'实际目的地');click('JourneyMap/Btn_Move','原地形费用实际消费',{'movement':remaining,'journey.position':pos},True);click('JourneyMap/Btn_CloseMap','继续同一回合')
def play(card,choice):
    click('Hand/Btn_Card_'+card+'_0','真实原牌');click('Inspector/Btn_Choice_'+choice,'原效果');click('Inspector/Btn_Play','正式费用与原效果')
def finish(turns,ranged=False):
    for _ in range(4 if ranged else 1):click('Inspector/Btn_Next','关闭实际战斗阶段')
    click('Inspector/Btn_EndTurn','同玩家正式结束回合',{'journey.completedTurns':turns},True)
def boost(soul=False):
    click('Btn_Skills','原英雄技能');click('Skills/Btn_Skill_0','原技能')
    click('Inspector/Btn_SoulContribution' if soul else 'Inspector/Btn_UseSkill','选择实际贡献')
    click('ContributionSkill/Btn_Part_0','选择真实第一笔元素贡献',capture=True)
    click('ContributionSkill/Btn_Confirm','原技能只加值所选贡献',{'power.blocks':'Ice:4','soul.pending':'False'} if soul else {'power.attacks':'Ranged:Fire:4'} if steps and bull and not any(s['label']=='第二场原兽人' for s in steps) else {'power.attacks':'Melee:Physical:3'},True)
    click('Btn_Skills','返回原牌')
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','原英雄构筑'));click('CampShade/Camp/Btn_Loadout','既有原牌起点')
for _ in range(2):click('CampShade/Picker/Btn_NextPage','下一页')
click('CampShade/Picker/Btn_Select_39','红蓝各一晶')
if leader:
    click('CampShade/Camp/Btn_ChooseUnits','实际原部队');click('CampShade/UnitPicker/Btn_Unit_army_007','原火法师');click('CampShade/UnitPicker/Btn_Back','确认合法指挥槽')
p='CampShade/DeedPicker/'
click('CampShade/Camp/Btn_ChooseCards','原七牌');click(p+'Btn_ClearDeeds','清空旧构筑')
for c in ['000','001','007','008']:click(p+'Btn_ChooseDeed_basic_card_'+c,'选原基础行动')
click(p+'Btn_DeedNext','下一页')
for c in (['020','021'] if soul else ['014','021','022'] if leader else ['014','021']):click(p+'Btn_ChooseDeed_basic_card_'+c,'选原基础行动')
if not leader:
    click(p+'Btn_DeedKind_1','原高级行动');click(p+'Btn_ChooseDeed_advanced_card_'+('001' if soul else '000'),'原元素箭矢')
click(p+'Btn_DoneDeeds','确认原卡');click('CampShade/Camp/Btn_SmallDeckHeroBuild','合法英雄');click('CampShade/HeroBuild/Btn_Hero_'+hero,'本英雄原技能');click('CampShade/HeroBuild/Btn_Level_4','等级4合法构筑')
for i in ([0] if soul else [4] if bull else [2,6]):click('CampShade/HeroBuild/Btn_Skill_'+str(i),'已合法取得原技能')
click('CampShade/HeroBuild/Btn_Begin','同一玩家开始',{'hero.id':hero,'hero.skills':2 if leader else 1});click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','正式第一回合')
if soul:
    hand();support();click('JourneySupport/Btn_OutsideSkill_0','灵魂向导');click('JourneySupport/Btn_UseOutsideSkill','移动1并保存一次格挡许可',{'movement':1,'soul.pending':'True'});closesupport();click('JourneyHand/Btn_Hand_1','真实宁静横置');click('JourneyHand/Btn_SidewaysMove','原横置移动1');closehand();move('plains',0,'0,1')
    click('Btn_JourneyEncounter_6','原火焰骷髅');click('Inspector/Btn_Next','正式格挡阶段');play('basic_card_020','block');boost(True)
    click('Enemy_0/Btn_Target','真实火焰攻击');click('Inspector/Btn_Resolve','寒冰格挡4挡火3');click('Inspector/Btn_Next','正式承伤');click('Inspector/Btn_Next','原格挡消费者',{'wounds':0})
    play('advanced_card_001','strong');play('basic_card_021','attack');click('Enemy_0/Btn_Target','同一原骷髅');click('Inspector/Btn_Resolve','冰攻击3加物攻2低效1击殺',{'kills':1,'blue':0},True);finish(1)
    click('Btn_BeginTurn','同玩家第二回合');hand();support();click('JourneySupport/Btn_OutsideSkill_0','同技能恢复本回合次数');click('JourneySupport/Btn_UseOutsideSkill','原移动1',{'movement':1,'soul.pending':'True'});closesupport();click('JourneyHand/Btn_Hand_0','实际保留行进');click('JourneyHand/Btn_OutsideChoice_base','原移动2');click('JourneyHand/Btn_PlayOutside','真实移动合计3');closehand();move('forest',0,'1,0');click('Btn_EndTurn','正式回合清未用格挡许可',{'journey.completedTurns':2,'soul.pending':'False'},True)
elif bull:
    click('Btn_JourneyEncounter_4','原狼骑兵');play('advanced_card_000','strong');boost();click('Enemy_0/Btn_Target','实际狼骑兵');click('Inspector/Btn_Resolve','火远程4实际击杀',{'kills':1,'red':0},True);finish(1,True)
    click('Btn_BeginTurn','同玩家第二回合');click('Btn_JourneyEncounter_0','第二场原兽人')
    for _ in range(3):click('Inspector/Btn_Next','原战斗阶段')
    click('Hand/Btn_Card_basic_card_014_0','原耐力横置');click('Inspector/Btn_Sideways','实际原牌普通攻击1');boost();click('Enemy_0/Btn_Target','真实原兽人');click('Inspector/Btn_Resolve','横置原牌攻击3真实击杀',{'kills':1},True);finish(2)
else:
    hand();click('JourneyHand/Btn_Hand_0','原行进');click('JourneyHand/Btn_OutsideChoice_base','原移动2');click('JourneyHand/Btn_PlayOutside','实际移动资源');closehand();move('plains',0,'0,1')
    click('Btn_JourneyEncounter_4','原狼骑兵');click('Btn_Skills','原英雄技能');click('Skills/Btn_NextSkill','领导');click('Skills/Btn_Skill_1','选择领导');click('Inspector/Btn_UseSkill','选择实际部队能力');click('Units/Btn_Unit_0','同一火法师',capture=True);click('Inspector/Btn_UnitAbility_0','原火远程3加领导1',{'power.attacks':'Ranged:Fire:4','units.ready':'False'},True);click('Enemy_0/Btn_Target','实际狼骑兵');click('Inspector/Btn_Resolve','真实火远程击杀',{'kills':1});finish(1,True)
    click('Btn_BeginTurn','同玩家第二回合');hand();support();click('JourneySupport/Btn_OutsideSkill_0','原重整旗鼓');click('JourneySupport/Btn_SkillUnit_0','同一耗竭火法师');click('JourneySupport/Btn_OutsideOption_0','重整非治疗');click('JourneySupport/Btn_UseOutsideSkill','真实重整同部队',{'units.ready':'True','units.wounds':'0'},True);closesupport();closehand();click('Btn_JourneyEncounter_0','第二场原兽人')
    for _ in range(3):click('Inspector/Btn_Next','原战斗阶段')
    play('basic_card_021','attack');click('Btn_Skills','领导本回合可用');click('Skills/Btn_Skill_1','同原领导');click('Inspector/Btn_UseSkill','真实部队原能力');click('Units/Btn_Unit_0','重整后的同火法师');click('Inspector/Btn_UnitAbility_0','近战使用原远程仍只加1',{'power.attacks':'Melee:Physical:2,Ranged:Fire:4','units.ready':'False'},True);click('Enemy_0/Btn_Target','真实兽人');click('Inspector/Btn_Resolve','同部队真实第二战击杀',{'kills':1,'units.wounds':'0'},True);finish(2)
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf8')
print(hero,len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
