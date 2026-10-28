"""Real random original opening, battle fame, end-turn rewards, and later card consumer."""
import json
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原英雄实际战斗获名望，回合末升级先于补牌；同一技能/奖励牌继续使用') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def advance():click('Inspector/Btn_Next','推进正式战斗阶段')
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','原英雄起点'));click('CampShade/Camp/Btn_OriginalHero','原英雄选择')
click('CampShade/HeroStart/Btn_Hero_Tovak','原托瓦克');click('CampShade/HeroStart/Btn_StartHero','随机洗混原16牌',{'hero.level':1,'hero.skills':0,'hand':5,'deck.count':11,'hero.deckTotal':16})
click('Tactic_day_1/Btn_Tactic_day_1','原捷足先登');click('Btn_BeginTurn','实际第一回合')
click('Btn_JourneyEncounter_4','实际兽人狼骑兵：护甲4、攻击3、名望3')
advance();advance();click('Inspector/Btn_Next','英雄护甲2，实际承伤两张',{'wounds':2,'hand':7})
for i in range(4):
    click('Hand/#0','选择随机起手中第一张实际原牌');click('Inspector/Btn_Sideways','实际横置攻击',{'melee':i+1,'played':i+1,'hand':6-i})
click('Enemy_0/Btn_Target','原兽人狼骑兵');click('Inspector/Btn_Resolve','消费攻击4，名望3尚未升级',{'kills':1,'fame':3,'hero.level':1},True)
advance();click('Inspector/Btn_EndTurn','实际清理，停在升级选择，尚未补牌',{'journey.endingTurn':'True','upgrade.level':2,'upgrade.choices':2,'hero.level':1,'hero.skills':0,'hand':3,'deck.count':11,'played':0})
click('LevelReward/Btn_RewardSkill_0','选择本次随机翻出的原自身技能')
click('LevelReward/Btn_RewardAction_1','选择中间原高级行动',{'upgrade.offerCount':3},True)
click('LevelReward/Btn_ConfirmReward','同一玩家获得原技能与原高级行动后补牌',{'hero.level':2,'hero.skills':1,'upgrade.common':1,'upgrade.choices':0,'upgrade.offerCount':3,'upgrade.inHand':'True','journey.endingTurn':'False','hand':5,'deck.count':10,'hero.deckTotal':19,'journey.completedTurns':1},True)
click('Btn_BeginTurn','同一英雄第二回合');click('Btn_JourneyEncounter_0','升级后继续实际战斗',{'hero.level':2,'hero.skills':1,'upgrade.inHand':'True','journey.samePlayer':'True'})
advance();advance();advance()
click('Hand/#3','选择刚获得且已补入实际手牌的高级行动')
click('Inspector/Btn_Sideways','奖励牌正常横置消费到已打出区',{'upgrade.inHand':'False','upgrade.played':'True','melee':1,'hero.skills':1},True)
advance();click('Inspector/Btn_EndTurn','实际第二回合清理保留技能与所有牌',{'hero.level':2,'hero.skills':1,'hero.deckTotal':21,'journey.completedTurns':2,'journey.completedBattles':2})
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=180,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
