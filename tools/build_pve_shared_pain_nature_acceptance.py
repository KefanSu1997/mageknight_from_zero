"""Prepared only: original Pain/Nature, five actual alternating personal turns.
Does not synthesize wounds. --from N replays the prefix without duplicate evidence.
Only running this generator writes the config; creation of this file does not.
"""
import json, sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='原痛苦/自然共享：真实个人回合、手伤实体、攻击槽与正式消费者') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def shared():
    click('Btn_JourneyTools','正式当前回合工具');click('JourneyTools/Btn_SharedSkills','同一局中央标记')
def close():
    click('SharedSkills/Btn_Back','返回');click('JourneyTools/Btn_CloseTools','回到实际流程')
def finish_battle(turn,wounds):
    click('Inspector/Btn_Next','进入承伤');click('Inspector/Btn_Next','确认实际英雄承伤',{'wounds':wounds},True)
    click('Inspector/Btn_Next','结束本场不击杀原敌');click('Inspector/Btn_EndTurn','正式个人回合结束',{'journey.completedTurns':turn})
def wounded_opening(turn):
    click('Btn_JourneyEncounter_0','原兽人骷髅战士攻击4护甲3');click('Inspector/Btn_Next','格挡阶段')
    click('Hand/Btn_Card_basic_card_000_0','本人的实际移动原牌');click('Inspector/Btn_Sideways','横置格挡1记录真实出牌',{'block':1})
    # Do not claim a failed block as success: no Resolve click; actual attack remains 4.
    finish_battle(turn,2)
def movement_and_cumbersome(points):
    click('Hand/Btn_Card_basic_card_014_0','原耐力');click('Inspector/Btn_Choice_base','基础移动2供笨重')
    click('Inspector/Btn_Play','实际原牌产生移动2',{'movement':2})
    shared();click('SharedSkills/Btn_Enemy_0','同一只已获笨重原敌')
    for i in range(points):click('SharedSkills/Btn_Cumbersome','支付实际移动1降低同一攻击',{'movement':1-i,'enemies.attacks':2-i},i==points-1)
    close()
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','双英雄构筑'));click('CampShade/Camp/Btn_Loadout','原牌构筑')
for _ in range(2):click('CampShade/Picker/Btn_NextPage','下一页')
click('CampShade/Picker/Btn_Select_39','零部队，英雄亲自承伤')
p='CampShade/DeedPicker/'
click('CampShade/Camp/Btn_ChooseCards','选择两玩家各自的原牌');click(p+'Btn_ClearDeeds','清构筑选择')
for card in ['000','001','007','008']:click(p+'Btn_ChooseDeed_basic_card_'+card,'原基础牌')
click(p+'Btn_DeedNext','下一页')
for card in ['014','016','021']:click(p+'Btn_ChooseDeed_basic_card_'+card,'原基础牌')
click(p+'Btn_DoneDeeds','确认原牌');click('CampShade/Camp/Btn_SmallDeckHeroBuild','合法英雄构筑')
h='CampShade/HeroBuild/'
click(h+'Btn_Hero_Arythea','Arythea原痛苦仪式');click(h+'Btn_Level_2','名望3等级2一技能');click(h+'Btn_Skill_9','原技能9')
click(h+'Btn_AddCompanion','保留独立第一英雄');click(h+'Btn_Hero_Braevalar','Braevalar原自然复仇');click(h+'Btn_Skill_9','原技能9')
click(h+'Btn_Begin','开始同一局双英雄',{'journey.players':2})
click('Tactic_day_1/Btn_Tactic_day_1','Arythea先手');click('Tactic_day_4/Btn_Tactic_day_4','Braevalar后手')
click('Btn_BeginTurn','Arythea首回合',{'hero.id':'Arythea'});wounded_opening(1)
click('Btn_BeginTurn','Braevalar首回合',{'hero.id':'Braevalar'});wounded_opening(2)
click('Btn_BeginTurn','Arythea正式下一回合',{'hero.id':'Arythea','wounds':2})
shared();click('SharedSkills/Btn_Own_0','原痛苦仪式');click('SharedSkills/Btn_Card_0','第一张实际战斗伤');click('SharedSkills/Btn_Card_1','第二张实际战斗伤',capture=True)
click('SharedSkills/Btn_Confirm','去除两张真实伤并放中央',{'wounds':0,'shared.ids':'hero_skill_pool_000:09'},True);close()
click('Btn_JourneyHand','原非伤手牌');click('JourneyHand/Btn_Hand_0','实际第一非伤牌');click('JourneyHand/Btn_DiscardAtEnd','正式回合末弃牌满足回合活动');click('JourneyHand/Btn_CloseHand','返回')
click('Btn_EndTurn','Arythea结束第二个人回合',{'journey.completedTurns':3})
click('Btn_BeginTurn','Braevalar第二回合保留真实伤',{'hero.id':'Braevalar','wounds':2})
click('Btn_JourneyEncounter_0','同一存活原兽人再战');click('Inspector/Btn_Next','格挡阶段')
shared();click('SharedSkills/Btn_Token_0','领取Arythea真实中央痛苦');click('SharedSkills/Btn_Card_0','选择本人的一张战斗伤');click('SharedSkills/Btn_Sideways_Block','伤牌横置格挡')
click('SharedSkills/Btn_Confirm','真实伤入已打出区且格挡3',{'wounds':1,'block':3,'shared.count':0},True)
click('SharedSkills/Btn_Own_0','Braevalar原自然复仇');click('SharedSkills/Btn_Enemy_0','原兽人实际第一攻击',capture=True)
click('SharedSkills/Btn_Confirm','攻击4降3并获笨重',{'enemies.attacks':3,'shared.ids':'hero_skill_pool_001:09'},True);close()
movement_and_cumbersome(2)
click('Enemy_0/Btn_Target','实际剩余攻击1');click('Inspector/Btn_Resolve','用痛苦真实伤牌格挡3完成格挡')
finish_battle(4,1)
click('Btn_BeginTurn','Arythea第三个人回合领取自然',{'hero.id':'Arythea','wounds':0})
click('Btn_JourneyEncounter_0','实际存活原敌');click('Inspector/Btn_Next','格挡阶段')
shared();click('SharedSkills/Btn_Token_0','另一玩家领取自然原中央标记');click('SharedSkills/Btn_Enemy_0','实际原敌攻击槽')
click('SharedSkills/Btn_Confirm','领取自然，原攻击4降3',{'enemies.attacks':3,'shared.count':0},True);close()
movement_and_cumbersome(2)
finish_battle(5,1)
# All numeric expectations above derive from monster_032 attack4 / hero armor2,
# actual Nature reductions, and original Endurance base Move2. No execution evidence yet.
# Prefix replay intentionally retains identical operations and player/card instances.
if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from')+1])]:step['after']=[];step['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=420,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps))
