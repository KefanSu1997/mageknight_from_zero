"""Actual battle wounds -> rest discards, optional original healing, and same-deck next round."""
import json
import sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原规则第6页休息/缓慢恢复、第11页治疗；实际牌区与技能每轮次数') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def support():
    click('Btn_JourneyHand','战斗外实际手牌');click('JourneyHand/Btn_Support','休息与已学技能')
def advance():click('Inspector/Btn_Next','正式战斗阶段推进')
def custom(ids):
    click('CampShade/Camp/Btn_ChooseCards','选择合法演练原手牌');click('CampShade/DeedPicker/Btn_ClearDeeds','清除演练手牌选择')
    for n in ids:click('CampShade/DeedPicker/Btn_ChooseDeed_basic_card_'+f'{n:03}','选原卡')
    click('CampShade/DeedPicker/Btn_DoneDeeds','确认原牌构筑')
    click('CampShade/Camp/Btn_Journey','开始同一玩家连续旅程');click('Tactic_day_1/Btn_Tactic_day_1','捷足先登');click('Btn_BeginTurn','开始回合')
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','合法演练起点'));click('CampShade/Camp/Btn_Loadout','已有提神沐浴构筑')
click('CampShade/Picker/Btn_NextPage','第二页');click('CampShade/Picker/Btn_NextPage','第三页');click('CampShade/Picker/Btn_Select_46','等级2狼人鹰提神沐浴')
custom([0,1,2,7,8])
click('Btn_JourneyEncounter_0','原哨卫真实承伤');advance();advance();advance()
click('Hand/#0','选择实际普通牌');click('Inspector/Btn_Sideways','横置一牌满足正式回合要求');advance()
click('Inspector/Btn_EndTurn','受伤回合结束',{'wounds':2,'hand':6,'journey.completedTurns':1})
click('Btn_BeginTurn','下一回合才能休息');support()
click('JourneySupport/Btn_RestCard_0','选一张原非伤牌');click('JourneySupport/Btn_RestCard_4','选择其中一张真实伤牌')
click('JourneySupport/Btn_Rest','标准休息：弃一普通牌和一伤，保留另一伤',{'hand':4,'wounds':1,'journey.discardCount':3,'journey.rested':'True','journey.removedCount':0},True)
click('JourneySupport/Btn_OutsideSkill_0','选择原提神沐浴');click('JourneySupport/Btn_UseOutsideSkill','休息中仍可用原治疗技能',{'healing':1,'blue':2,'journey.outsideSkillUsed':'True'},True)
click('JourneySupport/Btn_BackSupport','返回治疗操作');click('JourneyHand/Btn_HealHero','花1治疗点去除剩余真实伤牌',{'wounds':0,'healing':0,'hand':3,'journey.discardCount':3})
click('JourneyHand/Btn_Support','返回休息页');click('JourneySupport/Btn_EndSupportTurn','正式回合结束不重整每轮技能',{'journey.completedTurns':2,'hand':3,'hero.deckTotal':6})
click('Btn_Announce','空牌库合法结束轮次');click('Btn_EndRound','普通牌与休息弃伤一起洗回：治疗的另一伤不回来',{'deck.count':6,'hand':0})
click('Btn_PrepareRound','同一牌库新轮抽五张');click('Tactic_night_1/Btn_Tactic_night_1','黑夜战术');click('Btn_BeginTurn','新轮恢复每轮技能次数');support()
click('JourneySupport/Btn_OutsideSkill_0','同一提神沐浴新轮可用',{'journey.outsideSkillUsed':'False'});click('JourneySupport/Btn_UseOutsideSkill','新轮实际再次产生治疗和蓝晶',{'healing':1,'blue':3,'journey.outsideSkillUsed':'True'},True)
click('JourneySupport/Btn_BackSupport','返回');click('JourneyHand/Btn_CloseHand','回到旅程')
normal_steps=steps
steps=[]
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','缓慢恢复独立合法起点'))
custom([0])
click('Btn_JourneyEncounter_0','原哨卫第二段演练');advance();advance();advance()
click('Hand/#0','最后一张普通牌');click('Inspector/Btn_Sideways','实际横置后手牌只剩创伤',{'hand':2,'wounds':2})
advance();click('Inspector/Btn_EndTurn','空牌库无法补入普通牌',{'hand':2,'wounds':2})
click('Btn_BeginTurn','只有伤牌的下回合');support();click('JourneySupport/Btn_RestCard_0','明确选一张伤牌')
click('JourneySupport/Btn_Rest','缓慢恢复只弃一伤',{'wounds':1,'hand':1,'journey.discardCount':2,'journey.rested':'True','journey.removedCount':0},True)
click('JourneySupport/Btn_EndSupportTurn','结束回合仍保留一伤');click('Btn_Announce','空牌库声明结束');click('Btn_EndRound','慢恢复并非治疗，两伤仍在牌库',{'deck.count':3})
click('Btn_PrepareRound','两张真实伤牌重新抽回',{'hand':3,'wounds':2,'hero.deckTotal':3},True)
if '--slow' not in sys.argv:steps=normal_steps
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=240,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
