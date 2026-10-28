"""Original hero setup plus one persistent 16-card journey; no fixed shuffle or state injection."""
import json
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原英雄16张牌、等级1零资源、实际弃牌补牌及跨轮实体守恒') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','打开配置'));click('CampShade/Camp/Btn_OriginalHero','七位原英雄选择')
heroes=[('Arythea','024,013'),('Tovak','020,027'),('Goldyx','006,019'),('Norowas','012,005'),('Wolfhawk','010,017'),('Krang','003,026'),('Braevalar','004,018')]
for hero,ids in heroes:
    click('CampShade/HeroStart/Btn_Hero_'+hero,'原英雄专属替换 '+hero,{'hero.preview':hero,'hero.previewCount':16,'hero.previewUnique':','.join('basic_card_'+i for i in ids.split(','))},hero=='Braevalar')
click('CampShade/HeroStart/Btn_StartHero','真正创建原Braevalar初始牌库',{'hero.id':'Braevalar','hero.standardStart':'True','hero.level':1,'hero.skills':0,'hero.commandSlots':1,'hero.deckTotal':16,'units.count':0,'hand':5,'deck.count':11,'red':0,'blue':0,'green':0},True)
click('Tactic_day_1/Btn_Tactic_day_1','原捷足先登');click('Btn_BeginTurn','原英雄第一回合')
click('Btn_JourneyHand','原起始手牌')
for n in range(2):
    click('JourneyHand/Btn_Hand_0','点选实际原牌横置');click('JourneyHand/Btn_SidewaysMove','原牌横置移动',{'movement':n+1,'hand':4-n,'hero.deckTotal':16})
click('JourneyHand/Btn_Hand_0','选择回合末要弃的实际牌');click('JourneyHand/Btn_DiscardAtEnd','标记回合末弃牌',{'journey.discardSelected':1,'hand':3})
click('JourneyHand/Btn_CloseHand','返回');click('Btn_JourneyMap','实际地图');click('JourneyMap/Btn_Hex_plains','相邻平原');click('JourneyMap/Btn_Move','消耗横置移动2',{'movement':0,'journey.position':'0,1'},True)
click('JourneyMap/Btn_CloseMap','返回');click('Btn_EndTurn','弃一留二再补至五',{'hand':5,'deck.count':8,'played':0,'hero.deckTotal':16,'journey.completedTurns':1,'hero.id':'Braevalar'})
for turn,remaining in [(2,3),(3,0)]:
    click('Btn_BeginTurn','同一玩家下一回合');click('Btn_JourneyHand','选择本回合要弃的五张普通牌')
    for i in range(5):
        click('JourneyHand/Btn_Hand_'+str(i),'选择实际手牌实例');click('JourneyHand/Btn_DiscardAtEnd','仅标记尚不弃牌',{'journey.discardSelected':i+1})
    click('JourneyHand/Btn_CloseHand','返回');click('Btn_EndTurn','实际弃牌并消费剩余牌库',{'hand':5 if remaining else 3,'deck.count':remaining,'hero.deckTotal':16,'journey.completedTurns':turn})
click('Btn_Announce','空牌库合法声明轮次结束');click('Btn_EndRound','同一批16张牌洗混进入黑夜',{'hero.deckTotal':16,'journey.dayPart':'Night'})
click('Btn_PrepareRound','新轮实际补五张',{'hand':5,'deck.count':11,'hero.deckTotal':16,'hero.id':'Braevalar','hero.skills':0,'units.count':0},True)
click('Tactic_night_1/Btn_Tactic_night_1','原黑夜战术');click('Btn_BeginTurn','黑夜回合')
click('Btn_JourneyEncounter_0','原英雄进入实际战斗',{'hero.id':'Braevalar','hero.deckTotal':16,'hero.level':1,'units.count':0,'journey.samePlayer':'True'})
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=240,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
