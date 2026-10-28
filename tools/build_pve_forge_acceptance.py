"""Original Goldyx forges: actual card/unit mana payment, round markers and overflow."""
import json
from pathlib import Path
steps=[]
loadout=encounter=rejects=0

def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原锻造：蓝晶与印刷临时魔力、实际消费、每轮次数及最终战斗') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def advance(phase,expected=None,capture=False):step('Inspector/Btn_Next','进入_'+phase,{'phase':phase,**(expected or {})},capture)
def play(n,expected,capture=False):
    step(f'Hand/Btn_Card_basic_card_{n:03}_0','选择原行动卡')
    step('Inspector/Btn_Choice_strong','选择原印刷强化')
    step('Inspector/Btn_Play','消费锻造资源发动原卡',expected,capture)
def skill_reject(expected):
    global rejects
    rejects+=1
    step('Inspector/Btn_UseSkill','本轮再次锻造拒绝',{'rejections':rejects,'skills.used':'True','ui.Feedback':'该技能本轮已经翻面使用',**expected})

for l,index,color,e in [(30,2,'white',0),(31,3,'green',10),(32,4,'red',0),(33,4,'red',0)]:
    capped=l==33
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','选择原锻造出战'))
    while loadout!=l:
        loadout=(loadout+1)%34;step('CampShade/Camp/Btn_Loadout','切换已学原技能',{'draftLoadout':loadout})
    while encounter!=e:
        encounter=(encounter+1)%13;step('CampShade/Camp/Btn_Encounter','切换原敌人',{'draftEncounter':encounter})
    step('CampShade/Camp/Btn_Start','开始原技能战斗',{'phase':'Ranged','hero.id':'Goldyx','skills.ids':f'hero_skill_pool_005:{index:02}',
        'skills.used':'False','blue':3 if capped else 0,'red':0,'tokens.'+color:0,'hand':4,'art.cards':'True','art.enemies':'True'})
    step('Btn_Skills','查看原技能');step('Skills/Btn_Skill_0','选择原技能牌面',{'ui.skillArtCount':1})
    step('Inspector/Btn_SkillChoice_0','选择印刷魔晶与魔力')
    step('Inspector/Btn_UseSkill','实际锻造蓝晶与临时魔力',{'blue':3 if capped else 1,'tokens.blue':1 if capped else 0,'tokens.'+color:1,
        'skills.used':'True','ui.status':f'护甲 2    名望 3    创伤 0\n晶体   红 0   蓝 {3 if capped else 1}   白 0   绿 0\n魔力   红 {1 if color=="red" else 0}   蓝 {1 if capped else 0}   白 {1 if color=="white" else 0}   绿 {1 if color=="green" else 0}'},True)
    skill_reject({'blue':3 if capped else 1,'tokens.'+color:1})
    step('Btn_Skills','返回真实手牌')
    if index==2:play(7,{'ranged':3,'tokens.white':0,'blue':1},True)
    elif index==3:
        step('Btn_Roster','选择原凶蛮僧侣');step('Units/Btn_Unit_0','选择实际部队',{'ui.unitArtCount':1})
        step('Inspector/Btn_UnitAbility_1','消费绿色魔力发动攻城',{'siege':4,'tokens.green':0,'green':0,'blue':1,'units.ready':'False'},True)
    else:
        advance('Block');play(16,{'block':5,'blue':3 if capped else 0,'tokens.blue':0,'tokens.red':1},capped)
        step('Enemy_0/Btn_Target','选择敌人攻击');step('Inspector/Btn_Resolve','用真实强化格挡保护英雄',{'ui.Feedback':'格挡成立：有效 5 ≥ 4。效果随承伤阶段结算。'})
        advance('AssignDamage');advance('Melee',{'wounds':0,'realWounds':0})
        play(21,{'melee':4,'tokens.red':0,'red':0,'blue':3 if capped else 0},True)
    step('Enemy_0/Btn_Target','选择真实攻击目标')
    step('Inspector/Btn_Resolve','结算实际击杀',{'kills':1,'fame':6 if index==3 else 5,
        'ui.Feedback':f'攻击结算：有效 {3 if index==2 else 4} / 护甲 {4 if index==3 else 3}\n击败 1，名望 +{3 if index==3 else 2}。'})
    if index!=4:
        advance('Block');advance('AssignDamage');advance('Melee')
    advance('End',{'won':'True','wounds':0,'realWounds':0,'fame':6 if index==3 else 5,'tokens.'+color:0,
        'blue':3 if capped else 0 if index==4 else 1,'skills.used':'True'},True)
    step('Btn_Skills','战后核对技能翻面');step('Skills/Btn_Skill_0','仍是本轮原技能',{'ui.skillArtCount':1})
    skill_reject({'tokens.'+color:0})

request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
