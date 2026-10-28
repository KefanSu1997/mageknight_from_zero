"""Original northern/red cape monks: both printed abilities in attack and block, eight battles."""
import json
from pathlib import Path

steps=[]
loadout=encounter=rejects=0
roster=False

def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原僧侣能力、印刷颜色费用、耗竭、独立元素和实际战斗') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))

def start(l,e):
    global loadout,encounter,roster
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','选择独立僧侣战斗'))
    while loadout!=l:
        loadout=(loadout+1)%15
        step('CampShade/Camp/Btn_Loadout','选择原僧侣部队',{'draftLoadout':loadout})
    while encounter!=e:
        encounter=(encounter+1)%8
        step('CampShade/Camp/Btn_Encounter','选择对应原敌人',{'draftEncounter':encounter})
    step('CampShade/Camp/Btn_Start','开始真实部队快照',{'phase':'Ranged','source.units':'army_2_005' if l==13 else 'army_2_007',
        'units.ready':'True','units.wounds':0,'hand':4,'fame':0,'red':2,'blue':2,'art.cards':'True','art.enemies':'True'})
    roster=False

def show_units(want):
    global roster
    if roster!=want:
        step('Btn_Roster','查看部队原卡' if want else '返回真实手牌')
        roster=want

def card(n):
    show_units(False)
    step(f'Hand/Btn_Card_basic_card_{n:03}_0','选择原卡')

def play(n,choice,expected=None):
    card(n);step('Inspector/Btn_Choice_'+choice,'选择原印刷效果');step('Inspector/Btn_Play','正式出牌',expected)

def sideways(expected=None):
    card(0);step('Inspector/Btn_Sideways','横置真实原卡',expected)

def advance(phase,extra=None,capture=False):step('Inspector/Btn_Next','进入_'+phase,{'phase':phase,**(extra or {})},capture)
def target():step('Enemy_0/Btn_Target','选择原敌人')
def block(effective,need):
    target();step('Inspector/Btn_Resolve','真实完整格挡',{'block':0,
        'ui.Feedback':f'格挡成立：有效 {effective} ≥ {need}。效果随承伤阶段结算。'})

for northern in (True,False):
    for elemental in (False,True):
        for isblock in (False,True):
            e=(6 if northern else 7) if elemental else 0
            start(13 if northern else 14,e)
            advance('Block')
            red=blue=2
            if not isblock:
                play(16,'strong',{'block':5,'blue':1});blue-=1
                if elemental and northern:sideways({'block':6})
                block(3 if elemental and northern else 2 if elemental else 5,3 if elemental and northern else 2 if elemental else 4)
                advance('AssignDamage');advance('Melee',{'wounds':0,'realWounds':0})
            show_units(True);step('Units/Btn_Unit_0','选择部队原图',{'ui.unitArtCount':1})
            if elemental:
                if northern:blue-=1
                else:red-=1
            key='block' if isblock else 'melee'
            value=4 if elemental else 3
            label=('北地' if northern else '赤袍')+('元素' if elemental else '普通')+('格挡' if isblock else '攻击')
            expected={key:value,'red':red,'blue':blue,'units.ready':'False','units.wounds':0}
            step('Inspector/Btn_UnitAbility_'+str(int(elemental)),label,expected,elemental)
            rejects+=1
            step('Inspector/Btn_UnitAbility_'+str(int(elemental)),'重复激活不再扣费或获得资源',{**expected,'rejections':rejects})
            if isblock:
                if not elemental:sideways({'block':4})
                block(4,3 if elemental and northern else 2 if elemental else 4)
                advance('AssignDamage');advance('Melee',{'wounds':0,'realWounds':0})
                play(21,'strong',{'melee':4,'red':red-1});red-=1
                if elemental:
                    play(11,'strong',{'ranged':3,'blue':blue-1});blue-=1
                    sideways({'melee':5})
            fame=(6 if isblock else 5) if elemental else 2
            effective=4 if elemental or isblock else 3
            armor=4 if elemental else 3
            target();step('Inspector/Btn_Resolve','元素与抗性参与实际击杀',{'kills':1,'fame':fame,
                'ui.Feedback':f'攻击结算：有效 {effective} / 护甲 {armor}\n击败 1，名望 +{fame}。'})
            advance('End',{'won':'True','fame':fame,'red':red,'blue':blue,'units.ready':'False','units.wounds':0,'wounds':0},True)

request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=360,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
