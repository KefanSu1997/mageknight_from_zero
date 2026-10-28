"""Original social skills and persistent chaos through real continuous hero turns."""
import json,sys
from pathlib import Path
mode=sys.argv[1];steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='原英雄归属、声誉轨修正/实际增减、影响招募承诺、轮盘持久与真实连续消费者') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def hand(index,choice='base',expected=None):
    click('Btn_JourneyHand','同一实际手牌');click('JourneyHand/Btn_Hand_'+str(index),'选择原卡');click('JourneyHand/Btn_OutsideChoice_'+choice,'原效果');click('JourneyHand/Btn_PlayOutside','原卡实际出牌',expected);click('JourneyHand/Btn_CloseHand','返回旅程')
def skill(index,option,expected=None,capture=False):
    click('Btn_JourneyHand','手牌与技能');click('JourneyHand/Btn_Support','原技能入口');click('JourneySupport/Btn_OutsideSkill_'+str(index),'实际自有技能');click('JourneySupport/Btn_OutsideOption_'+str(option),'原技能明确选项');click('JourneySupport/Btn_UseOutsideSkill','原技能实际发动',expected,capture);click('JourneySupport/Btn_BackSupport','返回');click('JourneyHand/Btn_CloseHand','保留同玩家')
def move(site,expected=None):
    click('Btn_JourneyMap','实际地图');click('JourneyMap/Btn_Hex_'+site,'实际目的地');click('JourneyMap/Btn_Move','实际消费移动',expected);click('JourneyMap/Btn_CloseMap','返回旅程')
def play(card,choice):
    click('Hand/Btn_Card_basic_card_'+card+'_0','原战斗牌');click('Inspector/Btn_Choice_'+choice,'原效果');click('Inspector/Btn_Play','实际出牌')
(click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','原英雄合法构筑'));click('CampShade/Camp/Btn_Loadout','实际原资源')
for _ in range(2):click('CampShade/Picker/Btn_NextPage','下一页')
click('CampShade/Picker/Btn_Select_'+('40' if mode=='krang' else '39'),'可重放原起点')
click('CampShade/Camp/Btn_ChooseCards','原卡构筑');p='CampShade/DeedPicker/';click(p+'Btn_ClearDeeds','清空旧构筑')
for c in ['000','001','007','008']:click(p+'Btn_ChooseDeed_basic_card_'+c,'原基础牌')
click(p+'Btn_DeedNext','下一页')
for c in ['016','021','022']:click(p+'Btn_ChooseDeed_basic_card_'+c,'原基础牌')
click(p+'Btn_DoneDeeds','确认原牌');click('CampShade/Camp/Btn_SmallDeckHeroBuild','合法自身技能')
hero='Krang' if mode=='krang' else 'Wolfhawk';ids=[3,5,7] if mode=='krang' else [3]
click('CampShade/HeroBuild/Btn_Hero_'+hero,'正确英雄');click('CampShade/HeroBuild/Btn_Level_'+('6' if mode=='krang' else '2'),'合法等级')
for s in ids:click('CampShade/HeroBuild/Btn_Skill_'+str(s),'已学原自身技能')
click('CampShade/HeroBuild/Btn_Begin','同玩家正式旅程',{'hero.id':hero,'hero.skills':len(ids),**({'skills.chaosColor':'Black'} if mode=='krang' else {})})
click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','正式第一回合');hand(0);move('plains',{'journey.position':'0,1','movement':0})
if mode=='krang':
    hand(5,'strong',{'influence':5,'reputation':-1})
    skill(1,1,{'skills.ignoreReputationTrack':'True','reputation':-1},True)
    skill(2,0,{'skills.chaosColor':'White','influence':7},True)
    click('Btn_JourneyInteraction','实际村庄');click('JourneyInteraction/Btn_BeginInteraction','忽略负声誉轨修正',{'influence':7,'reputation':-1})
    click('JourneyInteraction/Btn_Offer_army_2_006','实际农民');click('JourneyInteraction/Btn_Recruit','真实影响力消费4',{'influence':3,'units.count':1,'reputation':-1},True);click('JourneyInteraction/Btn_CloseInteraction','返回')
    click('Btn_EndTurn','正式次回合',{'journey.completedTurns':1,'skills.ignoreReputationTrack':'False','skills.chaosPending':'False'})
    click('Btn_BeginTurn','同一玩家第二回合');skill(0,2,{'tokens.green':1});skill(1,100,{'tokens.green':0,'skills.ignoreReputationTrack':'False','skills.flipped':'hero_skill_pool_003:03'},True)
    hand(1);move('camp',{'journey.position':'0,0'});click('Btn_JourneyEncounter_0','实际兽人');click('Inspector/Btn_Next','原格挡阶段');play('016','strong');click('Enemy_0/Btn_Target','实际攻击槽');click('Inspector/Btn_Resolve','真实原卡格挡5')
    for _ in range(2):click('Inspector/Btn_Next','实际剩余阶段')
    click('Btn_Skills','原混乱大师')
    for _ in range(2):click('Skills/Btn_NextSkill','翻到实际第三技能')
    click('Skills/Btn_Skill_2','持久轮盘');click('Inspector/Btn_SkillChoice_0','顺时针红攻击2');click('Inspector/Btn_UseSkill','正式轮盘攻击',{'skills.chaosColor':'Red','melee':2},True);click('Btn_Skills','原牌');play('021','attack');click('Enemy_0/Btn_Target','实际兽人');click('Inspector/Btn_Resolve','原技能与原牌攻击消费',{'kills':1,'melee':0,'wounds':0},True)
    click('Inspector/Btn_Next','战斗结束');click('Inspector/Btn_EndTurn','第二回合结束',{'journey.completedTurns':2,'skills.chaosPending':'False'});click('Btn_BeginTurn','第三回合')
    click('Btn_JourneyHand','真实余牌');click('JourneyHand/Btn_Support','正式休息');click('JourneySupport/Btn_RestCard_0','真实宁静材料');click('JourneySupport/Btn_Rest','正式休息');click('JourneySupport/Btn_BackSupport','返回');click('JourneyHand/Btn_CloseHand','旅程');click('Btn_EndTurn','未用混乱的正式回合结束',{'journey.completedTurns':3,'skills.chaosPending':'True','skills.chaosColor':'Red'},True)
    click('Btn_ChaosRotate','原规则可选空转',{'skills.chaosColor':'Gold','skills.chaosPending':'False','movement':0,'influence':0},True);click('Btn_BeginTurn','真实第四回合保持轮盘',{'journey.completedTurns':3,'skills.chaosColor':'Gold','journey.samePlayer':'True'},True)
else:
    click('Btn_JourneyEncounter_4','实际狼骑兵')
    for _ in range(3):click('Inspector/Btn_Next','正式阶段')
    play('021','strong');click('Enemy_0/Btn_Target','实际敌人');click('Inspector/Btn_Resolve','实际受伤并击杀',{'kills':1,'wounds':2},True)
    click('Inspector/Btn_Next','战斗结束');click('Inspector/Btn_EndTurn','第一回合结束');click('Btn_BeginTurn','同一玩家第二回合')
    skill(0,1,{'influence':3,'skills.forbidInfluenceRecruit':'True'},True);hand(4,'base',{'influence':5})
    click('Btn_JourneyInteraction','实际村庄');click('JourneyInteraction/Btn_BeginInteraction','正式交涉');click('JourneyInteraction/Btn_Offer_army_2_006','实际供应农民');click('JourneyInteraction/Btn_Recruit','承诺拒绝实际影响招募',{'rejections':1,'units.count':0,'influence':5},True)
    click('JourneyInteraction/Btn_BuyHealing','实际支付3买治疗',{'influence':2,'healing':1});click('JourneyInteraction/Btn_CloseInteraction','返回');click('Btn_JourneyHand','实际伤牌');click('JourneyHand/Btn_HealHero','治疗真实伤牌',{'wounds':1,'healing':0},True);click('JourneyHand/Btn_CloseHand','返回');click('Btn_EndTurn','承诺到回合末',{'journey.completedTurns':2,'skills.forbidInfluenceRecruit':'False'});click('Btn_BeginTurn','第三回合')
    skill(0,0,{'influence':1,'skills.forbidInfluenceRecruit':'False'});hand(2,'base',{'influence':3})
    click('Btn_JourneyHand','真实普通材料');click('JourneyHand/Btn_Hand_0','宁静');click('JourneyHand/Btn_SidewaysInfluence','原横置影响1',{'influence':4});click('JourneyHand/Btn_CloseHand','返回')
    click('Btn_JourneyInteraction','实际村庄');click('JourneyInteraction/Btn_BeginInteraction','新回合交涉');click('JourneyInteraction/Btn_Offer_army_2_006','同供应农民');click('JourneyInteraction/Btn_Recruit','原影响1与原牌真实招募',{'influence':0,'units.count':1,'skills.influenceRecruited':'True'},True);click('JourneyInteraction/Btn_CloseInteraction','返回');click('Btn_EndTurn','连续三回合完成',{'journey.completedTurns':3,'journey.completedBattles':1},True)
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
if '--art-fix' in sys.argv:
    for index in [43,71]:
        steps[index]['after']=[dict(key='ui.truncated',expected='',rule='修正克朗第六技能原图页码后定向核对')]
        steps[index]['skipScreenshot']=False
if '--feedback-fix' in sys.argv:
    steps=steps[:60]
    for s in steps:s['after']=[];s['skipScreenshot']=True
    steps[-1]['after']=[dict(key='ui.truncated',expected='',rule='特立独行真实拒绝的原因与可治疗范围清晰可见')]
    steps[-1]['skipScreenshot']=False
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(mode,len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
