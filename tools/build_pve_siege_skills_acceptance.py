"""Arythea/Goldyx siege choices and Norowas day/night sniper in the actual battle UI."""
import json
from pathlib import Path
steps=[]
loadout=encounter=rejects=0
def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原英雄技能：独立攻城/元素/昼夜、实际操作与回合次数') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def start(l,e,hero,pool,index):
    global loadout,encounter
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','选择原英雄技能出战'))
    while loadout!=l:
        loadout=(loadout+1)%27;step('CampShade/Camp/Btn_Loadout','选择已学原技能',{'draftLoadout':loadout})
    while encounter!=e:
        encounter=(encounter+1)%13;step('CampShade/Camp/Btn_Encounter','切换原敌人',{'draftEncounter':encounter})
    step('CampShade/Camp/Btn_Start','开始原英雄战斗',{'hero.id':hero,'skills.ids':pool+f':{index:02}','skills.used':'False','phase':'Ranged',
        'hand':4,'fame':3,'red':1,'blue':1,'art.cards':'True','art.enemies':'True','dayPart':'Night' if e==5 else 'Day'})
def skill(option,key,value):
    global rejects
    step('Btn_Skills','查看原技能');step('Skills/Btn_Skill_0','选择原技能牌面',{'ui.skillArtCount':1})
    step('Inspector/Btn_SkillChoice_'+str(option),'选择原技能分支')
    step('Inspector/Btn_UseSkill','正式发动原技能',{key:value,'skills.used':'True','red':1,'blue':1},True)
    rejects+=1
    step('Inspector/Btn_UseSkill','同回合重复技能拒绝',{key:value,'skills.used':'True','rejections':rejects,'ui.Feedback':'该技能本回合已经使用'})
def advance(p,expected=None,capture=False):step('Inspector/Btn_Next','进入_'+p,{'phase':p,**(expected or {})},capture)
def attack(power,armor,reward):
    step('Enemy_0/Btn_Target','选择原敌人');step('Inspector/Btn_Resolve','结算真实击杀',{'kills':1,'fame':3+reward,
        'ui.Feedback':f'攻击结算：有效 {power} / 护甲 {armor}\n击败 1，名望 +{reward}。'},True)
def finish(fame,blue,unit):
    advance('Block');advance('AssignDamage');advance('Melee',{'wounds':0,'realWounds':0})
    advance('End',{'won':'True','fame':fame,'wounds':0,'realWounds':0,'skills.used':'True','red':1,'blue':blue,
        **({'units.ready':'False','units.wounds':0} if unit else {})},True)

for l,hero,pool,index in [(24,'Arythea','hero_skill_pool_000',1),(25,'Goldyx','hero_skill_pool_005',0)]:
    for option in (0,1):
        start(l,10,hero,pool,index);skill(option,'siege',1)
        step('Btn_Roster','打开原投石车');step('Units/Btn_Unit_0','选择原部队',{'ui.unitArtCount':1})
        step('Inspector/Btn_UnitAbility_0','普通攻城与技能独立叠加',{'siege':4,'ranged':0,'units.ready':'False','red':1,'blue':1})
        attack(4,4,3);finish(6,1,True)

for night in (False,True):
    start(26,5 if night else 0,'Norowas','hero_skill_pool_002',1);skill(0,'ranged',1 if night else 2)
    step('Btn_Skills','返回原手牌');step('Hand/Btn_Card_basic_card_011_0','选择原利斧')
    step('Inspector/Btn_Choice_'+('strong' if night else 'base'),'选择原利斧效果')
    step('Inspector/Btn_Play','支付实际费用并合并远程',{'ranged':4 if night else 3,'blue':0 if night else 1})
    attack(4 if night else 3,3,3 if night else 2);finish(6 if night else 5,0 if night else 1,False)

request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
