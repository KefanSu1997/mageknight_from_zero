"""Real skill-only rejection, selected discard, rest, empty declaration and new-round play."""
import json
import sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原规则第6页：每回合至少打出或弃一牌；空手空库声明结束；第11页实际弃牌与补牌') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def support():
    click('Btn_JourneyHand','实际手牌');click('JourneyHand/Btn_Support','休息与已学技能')
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','合法构筑起点'));click('CampShade/Camp/Btn_Loadout','原已学技能构筑')
click('CampShade/Picker/Btn_NextPage','第二页');click('CampShade/Picker/Btn_NextPage','第三页');click('CampShade/Picker/Btn_Select_46','原提神沐浴')
click('CampShade/Camp/Btn_ChooseCards','选择实际原牌');click('CampShade/DeedPicker/Btn_ClearDeeds','清除旧选择')
for card in ('000','001'):click('CampShade/DeedPicker/Btn_ChooseDeed_basic_card_'+card,'选择原普通牌')
click('CampShade/DeedPicker/Btn_DoneDeeds','两张原牌起点');click('CampShade/Camp/Btn_Journey','开始连续旅程')
click('Tactic_day_1/Btn_Tactic_day_1','原捷足先登');click('Btn_BeginTurn','开始第一回合',{'hand':2,'journey.cardActions':0})
click('Btn_EndTurn','无出牌弃牌拒绝空过',{'rejections':1,'journey.turnActive':'True','journey.completedTurns':0,'hand':2})
support();click('JourneySupport/Btn_OutsideSkill_0','原提神沐浴');click('JourneySupport/Btn_UseOutsideSkill','产生真实治疗与蓝晶',{'healing':1,'blue':2,'journey.outsideSkillUsed':'True','journey.cardActions':0})
click('JourneySupport/Btn_EndSupportTurn','只使用技能不能空过，保留资源与次数',{'rejections':2,'healing':1,'blue':2,'journey.outsideSkillUsed':'True','journey.turnActive':'True','journey.completedTurns':0,'hand':2},True)
click('JourneySupport/Btn_BackSupport','返回真实手牌');click('JourneyHand/Btn_Hand_0','选择实际非伤牌');click('JourneyHand/Btn_DiscardAtEnd','标记回合末弃牌，暂不移动牌',{'hand':2,'journey.discardCount':0,'journey.cardActions':0})
click('JourneyHand/Btn_CloseHand','返回');click('Btn_EndTurn','合法弃牌完成，空牌库不凭空补牌',{'hand':1,'journey.discardCount':1,'journey.cardActions':1,'journey.completedTurns':1,'journey.turnActive':'False','healing':0,'blue':2})
click('Btn_BeginTurn','同一玩家下回合重新计数',{'journey.cardActions':0,'hand':1});support()
click('JourneySupport/Btn_OutsideSkill_0','每轮技能不因回合刷新',{'journey.outsideSkillUsed':'True'})
click('JourneySupport/Btn_EndSupportTurn','上回合弃牌不能充当本回合操作',{'rejections':3,'journey.turnActive':'True','journey.completedTurns':1,'blue':2})
click('JourneySupport/Btn_RestCard_0','最后一张实际普通牌');click('JourneySupport/Btn_Rest','合法休息计入实际弃牌',{'hand':0,'journey.discardCount':2,'journey.cardActions':1,'journey.rested':'True'})
click('JourneySupport/Btn_EndSupportTurn','结束第二回合，手牌牌库均空',{'journey.completedTurns':2,'journey.turnActive':'False','hand':0,'deck.count':0,'journey.canAnnounce':'True'},True)
click('Btn_Announce','以声明代替空回合');click('Btn_EndRound','两张原牌回到实际牌库',{'deck.count':2,'hero.deckTotal':2})
click('Btn_PrepareRound','同一两张牌抽回');click('Tactic_night_1/Btn_Tactic_night_1','原黑夜战术');click('Btn_BeginTurn','新轮真实开回合',{'hand':2,'journey.cardActions':0})
support();click('JourneySupport/Btn_OutsideSkill_0','新轮正式恢复次数',{'journey.outsideSkillUsed':'False'});click('JourneySupport/Btn_BackSupport','返回真实牌')
click('JourneyHand/Btn_Hand_0','实际原手牌');click('JourneyHand/Btn_SidewaysMove','实际横置亦满足规则',{'hand':1,'journey.cardActions':1,'movement':1})
click('JourneyHand/Btn_CloseHand','返回回合');click('Btn_EndTurn','合法出牌完成，牌区守恒',{'journey.completedTurns':3,'hand':1,'journey.discardCount':1,'hero.deckTotal':2},True)
# Replay real inputs to restore the failed boundary without repeating prior checks/captures.
if '--tail' in sys.argv:
    for step in steps[:42]:
        step['after']=[];step['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=200,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
