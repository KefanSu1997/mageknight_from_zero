"""Original enemy-facing skills, real battle wounds and next-turn movement."""
import json,sys
from pathlib import Path
hero=sys.argv[1] if len(sys.argv)>1 else 'Krang'
skills={'Braevalar':[0],'Krang':[1,8],'Wolfhawk':[6]}[hero]
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
click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','正式第一回合');click('Btn_JourneyEncounter_4','原狼骑兵')
click('Inspector/Btn_Next','格挡阶段')
if hero!='Braevalar':
    click('Btn_Skills','打开原技能')
    if hero=='Krang':click('Skills/Btn_NextSkill','下一原技能诅咒')
    click('Skills/Btn_Skill_'+str(1 if hero=='Krang' else 0),'选择原诅咒或嘲讽')
    click('Inspector/Btn_SkillChoice_'+('0' if hero=='Krang' else '1'),'原攻击模式');click('Enemy_0/Btn_Target','实际敌人',capture=hero=='Wolfhawk')
    click('Inspector/Btn_UseSkill','只调整实际攻击',{'enemies.attacks':'2' if hero=='Krang' else '5','enemies.armor':'4' if hero=='Krang' else '2'},True)
    click('Btn_Skills','返回手牌')
click('Inspector/Btn_Next','实际分伤阶段')
if hero!='Wolfhawk':
    click('Btn_Skills','打开减伤技能')
    if hero=='Krang':click('Skills/Btn_NextSkill','回到身经百战')
    click('Skills/Btn_Skill_0','原英雄减伤');click('Inspector/Btn_SkillChoice_'+('1' if hero=='Braevalar' else '0'),'对应物理承伤模式')
    if hero=='Krang':click('Enemy_0/Btn_Target','选择本次实际攻击')
    click('Inspector/Btn_UseSkill','减伤等待正式承伤')
    click('Btn_Skills','返回真实手牌')
wounds={'Braevalar':1,'Krang':0,'Wolfhawk':3}[hero]
click('Inspector/Btn_Next','正式承伤消费技能',{'wounds':wounds,'phase':'Melee'},True)
click('Hand/Btn_Card_basic_card_021_0','原狂怒');click('Inspector/Btn_Choice_attack','原攻击2');click('Inspector/Btn_Play','贡献实际攻击',{'melee':2})
if hero!='Wolfhawk':
    for card in ['014','008']:
        click('Hand/Btn_Card_basic_card_'+card+'_0','原牌横置');click('Inspector/Btn_Sideways','实际攻击追加')
click('Enemy_0/Btn_Target','实际原狼骑兵');click('Inspector/Btn_Resolve','原攻击完成击杀',{'kills':1},True)
click('Inspector/Btn_Next','战斗结束');click('Inspector/Btn_EndTurn','真实回合结束保留伤牌',{'journey.completedTurns':1,'wounds':wounds})
click('Btn_BeginTurn','同一玩家次回合');click('Btn_JourneyHand','真实保留手牌');click('JourneyHand/Btn_Hand_0','原行进');click('JourneyHand/Btn_OutsideChoice_base','原移动2');click('JourneyHand/Btn_PlayOutside','实际移动资源',{'movement':2});click('JourneyHand/Btn_CloseHand','地图')
click('Btn_JourneyMap','同一旅程地图');click('JourneyMap/Btn_Hex_plains','实际村庄');click('JourneyMap/Btn_Move','支付原地形2',{'movement':0,'journey.position':'0,1'},True);click('JourneyMap/Btn_CloseMap','返回');click('Btn_EndTurn','连续两回合保持真实伤牌',{'journey.completedTurns':2,'journey.completedBattles':1,'wounds':wounds})
if '--from' in sys.argv:
    for step in steps[:int(sys.argv[sys.argv.index('--from')+1])]:step['after']=[];step['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf8')
print(hero,len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
