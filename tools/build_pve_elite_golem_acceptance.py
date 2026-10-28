"""Catapults and elemental golems: printed abilities, costs and real resistance damage."""
import json
from pathlib import Path
steps=[]
loadout=encounter=rejects=0
roster=False
def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原投石车/元素魔像：印刷费用、类型、抗性承伤与实际击杀') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def start(l,e,id):
    global loadout,encounter,roster
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','选择原精英部队'))
    while loadout!=l:
        loadout=(loadout+1)%24;step('CampShade/Camp/Btn_Loadout','选择原部队快照',{'draftLoadout':loadout})
    while encounter!=e:
        encounter=(encounter+1)%13;step('CampShade/Camp/Btn_Encounter','切换原遭遇',{'draftEncounter':encounter})
    step('CampShade/Camp/Btn_Start','开始原部队战斗',{'phase':'Ranged','source.units':id,'units.ready':'True','units.wounds':0,'units.armor':4,
        'red':2,'blue':2,'hand':4,'fame':0,'art.cards':'True','art.enemies':'True','dayPart':'Day'})
    roster=False
def show(want):
    global roster
    if roster!=want:step('Btn_Roster','查看原部队' if want else '返回原手牌');roster=want
def selectunit():
    show(True);step('Units/Btn_Unit_0','选择原部队牌面',{'ui.unitArtCount':1})
def ability(i,expected,capture=False):
    global rejects
    selectunit();step('Inspector/Btn_UnitAbility_'+str(i),'发动原能力',{'units.ready':'False','units.wounds':0,**expected},capture)
    rejects+=1
    step('Inspector/Btn_UnitAbility_'+str(i),'耗竭后拒绝再次激活',{'units.ready':'False','rejections':rejects,**expected})
def play(n,choice,expected=None):
    show(False);step(f'Hand/Btn_Card_basic_card_{n:03}_0','选择原卡');step('Inspector/Btn_Choice_'+choice,'选择原效果');step('Inspector/Btn_Play','正式出牌',expected)
def sideways(expected):
    show(False);step('Hand/Btn_Card_basic_card_000_0','选择原行进');step('Inspector/Btn_Sideways','原卡横置',expected)
def advance(p,expected=None,capture=False):step('Inspector/Btn_Next','进入_'+p,{'phase':p,**(expected or {})},capture)
def target():step('Enemy_0/Btn_Target','选择原敌人')
def block(e,n):
    target();step('Inspector/Btn_Resolve','完整格挡',{'block':0,'ui.Feedback':f'格挡成立：有效 {e} ≥ {n}。效果随承伤阶段结算。'})
def attack(e,armor,fame):
    target();step('Inspector/Btn_Resolve','实际击杀',{'kills':1,'fame':fame,'ui.Feedback':f'攻击结算：有效 {e} / 护甲 {armor}\n击败 1，名望 +{fame}。'})
def end(fame,red,blue):
    advance('End',{'won':'True','fame':fame,'wounds':0,'realWounds':0,'units.wounds':0,'units.ready':'False','red':red,'blue':blue},True)

# Three printed siege branches; elemental ones attack the fortified Eagle in the opening phase.
for i in range(3):
    start(21,0 if i==0 else 10,'army_004');red=1 if i==1 else 2;blue=1 if i==2 else 2
    value=3 if i==0 else 5;fame=2 if i==0 else 3
    ability(i,{'siege':value,'red':red,'blue':blue},True);attack(value,3 if i==0 else 4,fame)
    advance('Block');advance('AssignDamage');advance('Melee',{'wounds':0,'realWounds':0});end(fame,red,blue)

# Both ordinary attack/block branches of each golem.
for ice in (False,True):
    for isblock in (False,True):
        start(23 if ice else 22,0,'army_012' if ice else 'army_006');advance('Block')
        if isblock:
            ability(0,{'block':3,'red':2,'blue':2});sideways({'block':4});block(4,4)
        else:
            play(16,'strong',{'block':5,'blue':1});block(5,4)
        advance('AssignDamage');advance('Melee',{'wounds':0,'realWounds':0})
        if isblock:play(21,'strong',{'melee':4,'red':1})
        else:ability(0,{'melee':3,'red':2,'blue':1})
        attack(4 if isblock else 3,3,2);end(2,1 if isblock else 2,2 if isblock else 1)

# Fire golem is legal in daytime; ice golem's printed attack is melee.
start(22,7,'army_006');ability(1,{'ranged':4,'red':1,'blue':2},True);attack(4,4,5)
advance('Block');advance('AssignDamage');advance('Melee',{'wounds':0,'realWounds':0});end(5,1,2)
start(23,6,'army_012');advance('Block');play(16,'strong',{'block':5,'blue':1});sideways({'block':6});block(3,3)
advance('AssignDamage');advance('Melee',{'wounds':0,'realWounds':0});ability(1,{'melee':6,'red':2,'blue':0},True)
attack(6,4,5);end(5,2,0)

# Elemental resistance absorbs the real original enemy attack before activating the same unwounded unit.
for ice in (False,True):
    id='army_012' if ice else 'army_006'
    start(23 if ice else 22,7 if ice else 6,id);advance('Block');advance('AssignDamage')
    target();selectunit();step('Inspector/Btn_AssignUnit','预留原元素魔像承伤',{'units.plan':'0:0:'+id,'units.wounds':0},True)
    advance('Melee',{'wounds':0,'realWounds':0,'units.wounds':0,'units.ready':'True'},True)
    ability(0,{'melee':3,'red':2,'blue':2});play(21,'strong',{'melee':7,'red':1});play(11,'base',{'ranged':1})
    attack(4,4,5);end(5,1,2)

request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=480,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
