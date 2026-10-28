"""Original Savage Monks and Utem Crossbowmen: six complete battles over formal rules."""
import json
from pathlib import Path

steps=[]
loadout=encounter=rejects=0
roster=False

def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原部队数值、费用、攻击类型与真实战斗结算') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))

def start(savage,special):
    global loadout,encounter,roster
    l=16 if savage else 17
    e=10 if savage and special else 0
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','选择原部队出战'))
    while loadout!=l:
        loadout=(loadout+1)%18
        step('CampShade/Camp/Btn_Loadout','切换部队配置',{'draftLoadout':loadout})
    while encounter!=e:
        encounter=(encounter+1)%11
        step('CampShade/Camp/Btn_Encounter','切换原版遭遇',{'draftEncounter':encounter})
    step('CampShade/Camp/Btn_Start','开始真实部队快照',{'phase':'Ranged','source.units':'army_2_008' if savage else 'army_2_012',
        'units.ready':'True','units.wounds':0,'hand':4,'fame':0,'red':1,'blue':1,'green':int(savage),
        'art.cards':'True','art.enemies':'True'})
    roster=False

def show_units(want):
    global roster
    if roster!=want:
        step('Btn_Roster','查看部队原卡' if want else '返回真实手牌');roster=want

def card(n):
    show_units(False);step(f'Hand/Btn_Card_basic_card_{n:03}_0','选择原卡')

def play(n,choice,expected=None):
    card(n);step('Inspector/Btn_Choice_'+choice,'选择原印刷效果');step('Inspector/Btn_Play','正式出牌',expected)

def sideways(expected=None):
    card(0);step('Inspector/Btn_Sideways','横置真实原卡',expected)

def advance(phase,extra=None,capture=False):step('Inspector/Btn_Next','进入_'+phase,{'phase':phase,**(extra or {})},capture)
def target():step('Enemy_0/Btn_Target','选择原敌人')

def use(savage,ability,key,power):
    global rejects
    show_units(True);step('Units/Btn_Unit_0','查看原部队与能力',{'ui.unitArtCount':1})
    expected={key:power,'units.ready':'False','units.wounds':0,'green':int(savage and not ability)}
    step('Inspector/Btn_UnitAbility_'+str(ability),('凶蛮' if savage else '弩手')+'_'+key,expected,True)
    rejects+=1
    step('Inspector/Btn_UnitAbility_'+str(ability),'重复激活必须拒绝',{**expected,'rejections':rejects})

for savage in (True,False):
    for mode in ('attack','block','special'):
        special=mode=='special';isblock=mode=='block'
        start(savage,special)
        red=blue=1
        if special:
            use(savage,1,'siege' if savage else 'ranged',4 if savage else 2)
            if not savage:play(11,'base',{'ranged':3})
        else:
            advance('Block')
            if isblock:
                use(savage,0,'block',3);sideways({'block':4})
            else:
                play(16,'strong',{'block':5,'blue':0});blue=0
            target();step('Inspector/Btn_Resolve','实际格挡',{'block':0,
                'ui.Feedback':f'格挡成立：有效 {4 if isblock else 5} ≥ 4。效果随承伤阶段结算。'})
            advance('AssignDamage');advance('Melee',{'wounds':0,'realWounds':0})
            if isblock:play(21,'strong',{'melee':4,'red':0});red=0
            else:use(savage,0,'melee',3)
        fame=3 if savage and special else 2
        power=4 if isblock or savage and special else 3
        armor=4 if savage and special else 3
        target();step('Inspector/Btn_Resolve','实际攻城或普通击杀',{'kills':1,'fame':fame,
            'ui.Feedback':f'攻击结算：有效 {power} / 护甲 {armor}\n击败 1，名望 +{fame}。'})
        if special:
            advance('Block');advance('AssignDamage');advance('Melee',{'wounds':0,'realWounds':0})
        advance('End',{'won':'True','fame':fame,'red':red,'blue':blue,'green':int(savage and not special),
            'units.ready':'False','units.wounds':0,'wounds':0,'realWounds':0},True)

request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
