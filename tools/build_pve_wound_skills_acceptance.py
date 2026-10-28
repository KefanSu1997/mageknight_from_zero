"""Actual battle wounds consumed by original hero skills across two formal turns."""
import json,sys
from pathlib import Path
hero=sys.argv[1] if len(sys.argv)>1 else 'Braevalar'
pain=hero=='Arythea'
skill_index=5 if pain else 8 if hero=='Braevalar' else 4
first_color=1 if hero=='Braevalar' else 0
second_color=0 if hero=='Braevalar' else 1
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='原伤牌实际选择、再生精确魔力与真实抽牌、痛苦之力一张横置2及正式回合牌区') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','原英雄伤牌技能构筑'));click('CampShade/Camp/Btn_Loadout','既有可重放起点')
for _ in range(2):click('CampShade/Picker/Btn_NextPage','下一页')
click('CampShade/Picker/Btn_Select_39','红蓝各一晶，无预配部队')
if pain:
    p='CampShade/DeedPicker/'
    click('CampShade/Camp/Btn_ChooseCards','七张实际原牌');click(p+'Btn_ClearDeeds','清空旧构筑')
    for card in ['000','001','007','008']:click(p+'Btn_ChooseDeed_basic_card_'+card,'选择原牌')
    click(p+'Btn_DeedNext','原基础牌第二页')
    for card in ['014','021','022']:click(p+'Btn_ChooseDeed_basic_card_'+card,'选择原牌')
    click(p+'Btn_DoneDeeds','确认原牌')
click('CampShade/Camp/Btn_SmallDeckHeroBuild','合法英雄自身技能');click('CampShade/HeroBuild/Btn_Hero_'+hero,'原英雄 '+hero);click('CampShade/HeroBuild/Btn_Level_2','等级2一自身技能');click('CampShade/HeroBuild/Btn_Skill_'+str(skill_index),'本英雄原技能')
if not pain:click('CampShade/HeroBuild/Btn_DeckSource','原16张牌库')
click('CampShade/HeroBuild/Btn_Begin','同一真实玩家开始',{'hero.id':hero,'hero.level':2,'hero.armor':2,'hero.skills':1})
click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','正式第一回合');click('Btn_JourneyEncounter_4','原狼骑兵')
for _ in range(3):click('Inspector/Btn_Next','实际战斗阶段')
click('Btn_Skills','选择原技能',{'wounds':2});click('Skills/Btn_Skill_0','本英雄技能')
if not pain:
    click('Inspector/Btn_SkillWound','实际伤牌选择');click('SkillWounds/Btn_Wound_1','选择第二张实际战伤');click('Inspector/Btn_SkillChoice_'+str(first_color),'选择实际费用颜色');click('Inspector/Btn_UseSkill','战斗内去除所选伤，抽实际原牌',{'wounds':1,'deck.count':10,'blue' if first_color==1 else 'red':0},True)
else:click('Inspector/Btn_UseSkill','许可仅一张伤牌横置为2',capture=True)
click('Btn_Skills','返回真实手牌')
if pain:
    click('Hand/Btn_Card_w_1','选择第二张同名战伤');click('Inspector/Btn_Sideways','一张伤牌横置攻击2',{'melee':2,'wounds':1},True)
click('Hand/Btn_Card_basic_card_021_0','原狂怒');click('Inspector/Btn_Choice_attack','基础攻击2');click('Inspector/Btn_Play','实际原牌贡献',{'melee':4 if pain else 2})
if not pain:
    # Both original heroes retain these cards with the same deterministic original shuffle.
    for card in ['014','004' if hero=='Braevalar' else '003']:
        click('Hand/Btn_Card_basic_card_'+card+'_0','实际原手牌横置');click('Inspector/Btn_Sideways','追加真实普通攻击')
click('Enemy_0/Btn_Target','原狼骑兵');click('Inspector/Btn_Resolve','实际普通攻击4击杀',{'kills':1,'fame':6},True);click('Inspector/Btn_Next','战斗结束');click('Inspector/Btn_EndTurn','正式结束回合及补牌',{'journey.completedTurns':1,'wounds':1})
click('Btn_BeginTurn','正式次回合同一玩家');click('Btn_JourneyHand','实际手牌');click('JourneyHand/Btn_Support','原技能回合次数重置');click('JourneySupport/Btn_OutsideSkill_0','选择本英雄技能',{'journey.outsideSkillUsed':'False'})
if not pain:
    click('JourneySupport/Btn_Wound_0','保留至次回合的实际伤牌');click('JourneySupport/Btn_OutsideOption_'+str(second_color),'另一原色费用');click('JourneySupport/Btn_UseOutsideSkill','次回合去除真实余伤并抽牌',{'wounds':0,'red':0,'blue':0,'deck.count':8},True)
else:click('JourneySupport/Btn_UseOutsideSkill','本回合新的一次许可')
click('JourneySupport/Btn_BackSupport','返回同一手牌')
if pain:
    click('JourneyHand/Btn_NextHand','实际余伤所在页');click('JourneyHand/Btn_Hand_6','余伤');click('JourneyHand/Btn_SidewaysMove','痛苦伤牌移动2',{'movement':2,'wounds':0},True)
else:
    click('JourneyHand/Btn_Hand_4','再生本次新抽原牌');click('JourneyHand/Btn_SidewaysMove','新抽原牌真实横置移动1',{'movement':1})
    click('JourneyHand/Btn_Hand_0','原敏捷基础移动2');click('JourneyHand/Btn_OutsideChoice_base','基础移动');click('JourneyHand/Btn_PlayOutside','原移动实际叠加',{'movement':3})
click('JourneyHand/Btn_CloseHand','返回地图');click('Btn_JourneyMap','实际已揭示地图');click('JourneyMap/Btn_Hex_plains','原平原村庄');click('JourneyMap/Btn_Move','消费移动费用2',{'movement':0 if pain else 1,'journey.position':'0,1'});click('JourneyMap/Btn_CloseMap','返回旅程');click('Btn_EndTurn','两回合保留真实去向',{'journey.completedTurns':2,'journey.completedBattles':1,'wounds':0,'fame':6,**({'journey.discardCount':3,'hero.deckTotal':9,'played':0} if pain else {})},True)
if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from')+1])]:step['after']=[];step['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf8')
print(hero,len(steps),'steps',sum(len(step['after']) for step in steps),'checks')
