"""Actual original Herbalists: postcombat healing/readiness and free mana consumed by siege."""
import json
from pathlib import Path
steps=[]
loadout=encounter=rejects=0
roster=False
def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原药草师费用、目标、真实伤牌、耗竭及战斗结算') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def start(mana=False):
    global loadout,encounter,roster
    l=43 if mana else 42;e=10 if mana else 0
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','选择原药草师出战'))
    while loadout!=l:
        loadout=(loadout+1)%44;step('CampShade/Camp/Btn_Loadout','切换出战',{'draftLoadout':loadout})
    while encounter!=e:
        encounter=(encounter+1)%13;step('CampShade/Camp/Btn_Encounter','切换遭遇',{'draftEncounter':encounter})
    step('CampShade/Camp/Btn_Start','开始原部队快照',{'phase':'Ranged','source.units':'army_2_002,'+('army_2_008' if mana else 'army_2_014'),
        'units.ready':'True,True','units.wounds':'0,0','fame':8,'green':0 if mana else 1,'tokens.green':0,'healing':0,'hand':4})
    roster=False
def show(want):
    global roster
    if roster!=want:step('Btn_Roster','查看原部队' if want else '返回原手牌');roster=want
def select_unit(i):
    show(True);step(f'Units/Btn_Unit_{i}','选择原部队',{'ui.unitArtCount':2})
def use(i,n,expected=None,capture=False):
    select_unit(i);step(f'Inspector/Btn_UnitAbility_{n}','发动原部队能力',expected,capture)
def card(n):
    show(False);step(f'Hand/Btn_Card_basic_card_{n:03}_0','选择原行动卡')
def play(n,expected):
    card(n);step('Inspector/Btn_Choice_strong','选择原强化效果');step('Inspector/Btn_Play','实际付费出牌',expected)
def advance(phase,expected=None,capture=False):step('Inspector/Btn_Next','进入_'+phase,{'phase':phase,**(expected or {})},capture)
def resolve(power,block=False):
    step('Enemy_0/Btn_Target','选择原敌人');fame=3 if encounter==10 else 2;armor=4 if encounter==10 else 3
    text=f'格挡成立：有效 {power} ≥ 4。效果随承伤阶段结算。' if block else f'攻击结算：有效 {power} / 护甲 {armor}\n击败 1，名望 +{fame}。'
    step('Inspector/Btn_Resolve','真实格挡' if block else '真实击杀',{'ui.Feedback':text,**({'block':0} if block else {'kills':1,'fame':8+fame})})
def own_target():
    step('Inspector/Btn_UnitSupportTarget','切换治疗或重整目标',{'support.target':0})
    step('Inspector/Btn_UnitSupportTarget','选择乌顿剑士',{'support.target':1})

for mode in ('heal_hero','heal_unit','ready','ready_wounded'):
    start();advance('Block')
    if mode=='heal_unit':play(16,{'block':5,'blue':0});resolve(5,True)
    elif mode.startswith('ready'):
        wound=mode=='ready_wounded'
        use(1,1 if wound else 0,{'block':6 if wound else 3,'units.ready':'True,False','units.wounds':'0,1' if wound else '0,0'})
        if not wound:card(0);step('Inspector/Btn_Sideways','真实横置补足格挡',{'block':4})
        resolve(6 if wound else 4,True)
    advance('AssignDamage');advance('Melee',{'wounds':2 if mode=='heal_hero' else 0,'realWounds':2 if mode=='heal_hero' else 0})
    if mode=='heal_unit':use(1,1,{'melee':6,'units.wounds':'0,1','units.ready':'True,False'});resolve(6)
    else:play(21,{'melee':4,'red':0});resolve(4)
    advance('End',{'won':'True','fame':10,'healing':0})
    select_unit(0)
    if mode!='heal_hero':own_target()
    ready=mode.startswith('ready')
    expected={'units.ready':'False,True' if ready else 'False,True' if mode=='heal_hero' else 'False,False',
        'green':1 if ready else 0,'healing':0 if ready else 2,'units.wounds':'0,1' if mode in ('heal_unit','ready_wounded') else '0,0'}
    step('Inspector/Btn_UnitAbility_'+('1' if ready else '0'),'战后重整' if ready else '绿色魔力生成治疗点',expected,True)
    rejects+=1;step('Inspector/Btn_UnitAbility_'+('1' if ready else '0'),'耗竭药草师不能重复发动',{**expected,'rejections':rejects})
    if mode=='heal_hero':
        step('Inspector/Btn_HealWound','治疗第一张真实手牌创伤',{'healing':1,'wounds':1,'realWounds':1})
        step('Inspector/Btn_HealWound','治疗第二张真实手牌创伤',{'healing':0,'wounds':0,'realWounds':0,'hand':3},True)
    elif mode=='heal_unit':
        step('Inspector/Btn_HealWound','按等级二消耗两点治疗',{'healing':0,'units.wounds':'0,0','units.ready':'False,False','units.canActivate':'False,False'},True)
    else:
        step('Units/Btn_Unit_1','核对重整后的真实部队',{'units.ready':'False,True','units.wounds':'0,1' if mode=='ready_wounded' else '0,0',
            'units.canActivate':'False,False' if mode=='ready_wounded' else 'False,True'},True)

start(True)
use(0,2,{'tokens.green':1,'green':0,'units.ready':'False,True'},True)
use(1,1,{'tokens.green':0,'green':0,'siege':4,'units.ready':'False,False'},True)
resolve(4);advance('Block');advance('AssignDamage');advance('Melee',{'wounds':0,'realWounds':0})
advance('End',{'won':'True','fame':11,'tokens.green':0,'green':0,'units.ready':'False,False'},True)
request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
