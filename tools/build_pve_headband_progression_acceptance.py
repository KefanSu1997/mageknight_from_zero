"""Original hero level reward -> real common skill -> headband and map consumer."""
import json,sys
from pathlib import Path
burn='--burn' in sys.argv
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原头环普通借用每回合技能两次；焚毁取得实际公共技能；真实升级、费用和移动消费') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
p='CampShade/DeedPicker/'
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','配置连续原英雄构筑'));click('CampShade/Camp/Btn_Loadout','现有显式随机重放');click('CampShade/Picker/Btn_NextPage','第二页');click('CampShade/Picker/Btn_NextPage','第三页');click('CampShade/Picker/Btn_Select_39','显式种子0')
click('CampShade/Camp/Btn_ChooseCards','选择原牌');click(p+'Btn_ClearDeeds','清空待选')
click(p+'Btn_ChooseDeed_basic_card_000','原行进');click(p+'Btn_DeedNext','基础第二页');click(p+'Btn_ChooseDeed_basic_card_021','原狂怒');click(p+'Btn_DeedKind_3','原神器');click(p+'Btn_DeedNext','神器第二页');click(p+'Btn_ChooseDeed_items_022','原启迪头环');click(p+'Btn_DoneDeeds','保留三张原牌')
click('CampShade/Camp/Btn_OriginalHero','选择本次构筑的原英雄');click('CampShade/HeroStart/Btn_Hero_Tovak','原托瓦克')
click('CampShade/HeroStart/Btn_ConfiguredHero','沿用原牌，等级1无技能部队',{'hero.id':'Tovak','hero.level':1,'hero.skills':0,'units.count':0,'hero.standardStart':'False','hand.ids':'basic_card_000,basic_card_021,items_022'},not burn)
click('Tactic_day_1/Btn_Tactic_day_1','捷足先登');click('Btn_BeginTurn','第一回合');click('Btn_JourneyEncounter_4','原狼骑兵')
click('Inspector/Btn_Next','格挡');click('Inspector/Btn_Next','承伤');click('Inspector/Btn_Next','实际两张伤牌',{'wounds':2})
click('Hand/Btn_Card_basic_card_021_0','原狂怒');click('Inspector/Btn_Choice_strong','红费强化');click('Inspector/Btn_Play','真实支付红晶产生攻击4',{'red':0,'melee':4});click('Enemy_0/Btn_Target','原狼骑兵');click('Inspector/Btn_Resolve','击败后名望3，尚未升级',{'kills':1,'fame':3,'hero.level':1});click('Inspector/Btn_Next','结束实际战斗')
click('Inspector/Btn_EndTurn','结束回合触发正式奖励',{'journey.endingTurn':'True','upgrade.choices':2})
click('LevelReward/Btn_RewardSkill_1','实际翻出的抗性弱化');click('LevelReward/Btn_RewardAction_0','原高级行动供应最底')
click('LevelReward/Btn_ConfirmReward','实际未选动力进入公共区',{'hero.level':2,'hero.skills':1,'supply.skills':'hero_skill_pool_007:00','upgrade.skillSources':'hero_skill_pool_007:04','hand':5},not burn)
click('Btn_BeginTurn','同一英雄第二回合');click('Btn_JourneyHand','原手牌');click('JourneyHand/Btn_Hand_1','实际头环');click('JourneyHand/Btn_PlayOutside','展开原普通与焚毁规则');click('SpecialArtifact/Btn_PublicSkill_0','选择公共动力技能')
if burn:click('SpecialArtifact/Btn_SpecialBurn','焚毁取得实际技能')
click('SpecialArtifact/Btn_ConfirmSpecial','取得技能尚未发动' if burn else '借用每回合技能两次',{'hero.skills':2 if burn else 1,'movement':0 if burn else 4,'supply.skills':'' if burn else 'hero_skill_pool_007:00','removed.ids':'items_022' if burn else ''},True)
if burn:
    click('JourneyHand/Btn_Support','检查实际已学技能');click('JourneySupport/Btn_OutsideSkill_1','实际取得的动力');click('JourneySupport/Btn_UseOutsideSkill','正式发动并记录每回合次数',{'movement':2,'journey.outsideSkillUsed':'True'},True);click('JourneySupport/Btn_BackSupport','返回手牌')
click('JourneyHand/Btn_CloseHand','回到实际地图');click('Btn_JourneyMap','查看地形');click('JourneyMap/Btn_Hex_plains','原平原村庄')
click('JourneyMap/Btn_Move','实际移动2消费',{'journey.position':'0,1','movement':0 if burn else 2},True);click('JourneyMap/Btn_CloseMap','返回旅程')
click('Btn_EndTurn','结束回合保留实际技能及神器去向',{'journey.completedTurns':2,'hero.skills':2 if burn else 1,'removed.ids':'items_022' if burn else '', 'supply.skills':'' if burn else 'hero_skill_pool_007:00','discard.ids':'basic_card_021' if burn else 'items_022,basic_card_021'})
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
if '--display-only' in sys.argv:
    steps=steps[:38]
    for i,s in enumerate(steps):
        s['after']=[dict(key='ui.truncated',expected='',rule='自定英雄来源标签及原头环、实际公共技能原图原文无截断')] if i in (16,37) else []
        s['skipScreenshot']=i not in (16,37)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=200,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
