"""Original enemy-facing skills, real battle wounds and next-turn movement."""
import json,sys
from pathlib import Path
hero=sys.argv[1] if len(sys.argv)>1 else 'Krang'
skills={'Krang':[2,3],'Wolfhawk':[5]}[hero]
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='原英雄减伤在部队分伤后实际消费；诅咒/嘲讽仅所选攻击，真实费用与回合牌区') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','原英雄构筑'));click('CampShade/Camp/Btn_Loadout','复用原牌起点')
for _ in range(2):click('CampShade/Picker/Btn_NextPage','下一页')
click('CampShade/Picker/Btn_Select_39','零部队起点')
p='CampShade/DeedPicker/'
click('CampShade/Camp/Btn_ChooseCards','实际七原牌');click(p+'Btn_ClearDeeds','清空旧选择')
for card in ['000','001','007','008']:click(p+'Btn_ChooseDeed_basic_card_'+card,'选择原牌')
click(p+'Btn_DeedNext','第二页')
for card in ['014','021','022']:click(p+'Btn_ChooseDeed_basic_card_'+card,'选择原牌')
click(p+'Btn_DoneDeeds','确认原牌');click('CampShade/Camp/Btn_SmallDeckHeroBuild','合法自身技能');click('CampShade/HeroBuild/Btn_Hero_'+hero,'原英雄')
click('CampShade/HeroBuild/Btn_Level_'+('4' if hero=='Krang' else '2'),'合法等级')
for i in skills:click('CampShade/HeroBuild/Btn_Skill_'+str(i),'已学自身技能')
click('CampShade/HeroBuild/Btn_Begin','同一真实玩家',{'hero.id':hero,'hero.skills':len(skills)})
click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','正式第一回合')
if hero=='Krang':
    click('Btn_JourneyHand','真实手牌');click('JourneyHand/Btn_Support','原技能');click('JourneySupport/Btn_OutsideSkill_1','萨满仪式');click('JourneySupport/Btn_OutsideOption_0','原红色标记');click('JourneySupport/Btn_UseOutsideSkill','萨满翻面得实际红魔力',{'tokens.red':1,'skills.flipped':'hero_skill_pool_003:03'},True);click('JourneySupport/Btn_BackSupport','返回');click('JourneyHand/Btn_CloseHand','旅程')
click('Btn_JourneyEncounter_4','原狼骑兵');click('Inspector/Btn_Next','原格挡阶段')
if hero=='Wolfhawk':
    click('Btn_Skills','原英雄技能');click('Skills/Btn_Skill_0','洞悉猎物');click('Enemy_0/Btn_Target','实际狼骑兵');click('Inspector/Btn_SkillChoice_101','原迅捷能力');click('Inspector/Btn_UseSkill','忽略原迅捷',{'skills.used':'True'},True);click('Btn_Skills','返回原手牌')
    click('Hand/Btn_Card_basic_card_021_0','原狂怒格挡');click('Inspector/Btn_Choice_block','基础格挡2');click('Inspector/Btn_Play','实际格挡2');click('Hand/Btn_Card_basic_card_008_0','原承诺横置');click('Inspector/Btn_Sideways','普通格挡再加1');click('Enemy_0/Btn_Target','原敌');click('Inspector/Btn_Resolve','去迅捷后实际格挡3成功',{'wounds':0},True)
click('Inspector/Btn_Next','正式分伤');click('Inspector/Btn_Next','实际近战',{'wounds':1 if hero=='Krang' else 0})
if hero=='Krang':
    click('Btn_Skills','原战斗狂乱');click('Skills/Btn_Skill_0','战斗狂乱');click('Inspector/Btn_SkillChoice_1','攻击4并翻面');click('Inspector/Btn_UseSkill','持久翻面攻击4',{'melee':4,'skills.flipped':'hero_skill_pool_003:02,hero_skill_pool_003:03'},True);click('Btn_Skills','实际原牌');click('Hand/Btn_Card_basic_card_021_0','原狂怒');click('Inspector/Btn_Choice_strong','红费强化4');click('Inspector/Btn_Play','实际萨满魔力付款',{'tokens.red':0,'red':1,'melee':8},True)
else:
    for card in ['001','007','014','022']:
        click('Hand/Btn_Card_basic_card_'+card+'_0','真实原牌横置');click('Inspector/Btn_Sideways','普通攻击贡献1')
click('Enemy_0/Btn_Target','原狼骑兵');click('Inspector/Btn_Resolve','真实攻击完成击杀',{'kills':1},True);click('Inspector/Btn_Next','战斗结束');click('Inspector/Btn_EndTurn','同玩家正式结束回合',{'journey.completedTurns':1})
click('Btn_BeginTurn','同玩家第二回合')
if hero=='Krang':
    click('Btn_JourneyHand','实际手牌');click('JourneyHand/Btn_Support','正式休息');click('JourneySupport/Btn_RestCard_0','真实行进作为休息材料');click('JourneySupport/Btn_Rest','实际休息只恢复狂乱',{'journey.rested':'True','skills.flipped':'hero_skill_pool_003:03'},True);click('JourneySupport/Btn_BackSupport','返回');click('JourneyHand/Btn_CloseHand','旅程');click('Btn_EndTurn','第二回合休息结束',{'journey.completedTurns':2})
    click('Btn_BeginTurn','第三回合');click('Btn_JourneyHand','真实保留牌');click('JourneyHand/Btn_Hand_1','原敏捷基础移动');click('JourneyHand/Btn_OutsideChoice_base','基础移动2');click('JourneyHand/Btn_PlayOutside','原移动资源');click('JourneyHand/Btn_CloseHand','旅程')
else:
    click('Btn_JourneyHand','实际手牌');click('JourneyHand/Btn_Hand_0','保留原行进');click('JourneyHand/Btn_OutsideChoice_base','基础移动2');click('JourneyHand/Btn_PlayOutside','真实移动');click('JourneyHand/Btn_CloseHand','地图')
click('Btn_JourneyMap','真实地图');click('JourneyMap/Btn_Hex_plains','原村庄');click('JourneyMap/Btn_Move','原移动费用2',{'movement':0,'journey.position':'0,1'});click('JourneyMap/Btn_CloseMap','返回')
if hero=='Krang':
    click('Btn_JourneyHand','当前手牌');click('JourneyHand/Btn_Support','原萨满恢复');click('JourneySupport/Btn_OutsideSkill_1','翻面萨满');click('JourneySupport/Btn_OutsideOption_100','使用本回合行动');click('JourneySupport/Btn_UseOutsideSkill','正式行动翻回萨满',{'skills.flipped':''},True);click('JourneySupport/Btn_BackSupport','返回');click('JourneyHand/Btn_CloseHand','旅程')
click('Btn_EndTurn','连续流程完成',{'journey.completedTurns':3 if hero=='Krang' else 2,'journey.completedBattles':1},True)
if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from')+1])]:step['after']=[];step['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf8')
print(hero,len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
