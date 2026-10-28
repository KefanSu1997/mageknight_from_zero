"""Focused continuous UI route. Original cards, actual costs, wounds and the same next-round deck."""
import json
from pathlib import Path
root='Canvas/BattleTable/'
steps=[]
def click(path,label,expected=None,capture=False):
    checks={'ui.truncated':''}
    checks.update(expected or {})
    steps.append(dict(buttonPath=root+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='同一原玩家/牌/部队/敌人实体；正式费用、行动、回合与轮次，不重建快照') for k,v in checks.items()]))
def card(id): click('Hand/Btn_Card_'+id+'_0','点选实际原卡 '+id)
def strong(id):
    card(id); click('Inspector/Btn_Choice_strong','选择原强化效果'); click('Inspector/Btn_Play','支付原费用并实际出牌')
def target(i):click(f'Enemy_{i}/Btn_Target','选择实际原敌人 '+str(i))
def next():click('Inspector/Btn_Next','正式战斗阶段推进')
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','打开出战配置'))
click('CampShade/Camp/Btn_Loadout','选择有两次真实攻击/格挡材料的原构筑')
click('CampShade/Picker/Btn_NextPage','构筑第二页')
click('CampShade/Picker/Btn_Select_20','灵魂收割者构筑')
click('CampShade/Camp/Btn_Journey','开始同一玩家连续旅程',{'journey.active':'True','hand':7,'journey.roundPrepared':'True','ui.tacticArtCount':6},True)
click('Tactic_day_1/Btn_Tactic_day_1','取得原捷足先登',{'journey.tactic':'day_1','journey.tacticResolved':'True','hand':7})
click('Btn_BeginTurn','正式开始第一回合',{'journey.turnActive':'True'})
click('Btn_JourneyEncounter_0','进入原遗迹哨卫',{'phase':'Ranged','journey.samePlayer':'True','red':2,'blue':2})
next();strong('basic_card_016');target(0);click('Inspector/Btn_Resolve','原格挡5消费抵挡攻击4',{'block':0,'blue':1})
next();next();strong('basic_card_021');target(0);click('Inspector/Btn_Resolve','原攻击4击败护甲3',{'fame':2,'red':1});next()
click('Inspector/Btn_EndTurn','正式结束第一回合，原两卡进弃牌',{'journey.completedTurns':1,'journey.completedBattles':1,'hand':5,'played':0,'deck.count':0,'red':1,'blue':1},True)
click('Btn_BeginTurn','保留原玩家开始第二回合',{'hand':5,'journey.turnActive':'True'})
click('Btn_JourneyEncounter_2','进入两个新的原哨卫标记',{'phase':'Ranged','journey.samePlayer':'True','red':1,'blue':1})
next();strong('basic_card_016');target(0);click('Inspector/Btn_Resolve','仅格挡其中一次攻击',{'blue':0})
next();click('Inspector/Btn_Next','未格挡原攻击4造成两张真实伤牌',{'wounds':2,'realWounds':2})
strong('basic_card_021');card('items_019');click('Inspector/Btn_ArtifactPlay','原灵魂收割者实际出牌，攻击3与原怒气4合计7',{'melee':7,'red':0})
target(0);target(1);click('Inspector/Btn_Resolve','合并攻击7击败两名总护甲6原敌人',{'kills':2,'fame':6,'white':2});next()
click('Inspector/Btn_EndTurn','保留两张伤牌与神器去向结束第二回合',{'journey.completedTurns':2,'journey.completedBattles':2,'hand':4,'played':0,'red':0,'blue':0},True)
click('Btn_Announce','牌库为空，在回合前声明轮末',{'journey.turnActive':'False'})
click('Btn_EndRound','正式结束白昼，原牌含伤牌洗回',{'journey.dayPart':'Night','journey.round':2,'hand':0,'deck.count':9})
click('Btn_PrepareRound','同一玩家准备黑夜轮次',{'journey.roundPrepared':'True','hand':5,'deck.count':4,'red':0,'blue':0,'ui.tacticArtCount':6},True)
click('Tactic_night_4/Btn_Tactic_night_4','取得原午夜冥想',{'journey.tactic':'night_4'})
click('Btn_Meditation','回合前选择冥想实际手牌')
click('Btn_TacticCard_0','选择一张实际手牌')
click('Btn_ConfirmTactic','先洗回再抽相同数量',{'hand':5,'deck.count':4,'journey.tacticSpent':'True'},True)
click('Btn_BeginTurn','原夜间战术处理后进入真实下一回合',{'journey.turnActive':'True','red':0,'blue':0})
click('Btn_JourneyTools','查看原战术与实际公共源',{'journey.source.count':3,'ui.tacticArtCount':1},True)
config=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=220,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(config,ensure_ascii=False,indent=2),encoding='utf-8')
print('steps',len(steps),'checks',sum(len(s['after']) for s in steps))
