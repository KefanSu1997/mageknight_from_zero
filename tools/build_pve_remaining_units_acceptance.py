"""Only previously unverified Golem/Gunner basic modes; preserve their earlier ice/ranged evidence."""
import json
from pathlib import Path

steps=[]
loadout=encounter=rejects=0
roster=False

def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原魔像/枪手缺失分支、元素格挡、正式费用与实际击杀') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))

def start(golem,fire):
    global loadout,encounter,roster
    l=18 if golem else 3
    e=7 if fire else 0
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','选择已有部队的未验收能力'))
    while loadout!=l:
        loadout=(loadout+1)%19
        step('CampShade/Camp/Btn_Loadout','选择原部队配置',{'draftLoadout':loadout})
    while encounter!=e:
        encounter=(encounter+1)%11
        step('CampShade/Camp/Btn_Encounter','选择原敌人遭遇',{'draftEncounter':encounter})
    step('CampShade/Camp/Btn_Start','开始实际出战',{'phase':'Ranged','source.units':'army_2_001' if golem else 'army_003',
        'units.ready':'True','units.wounds':0,'hand':4 if golem else 3,'red':2 if golem else 1,'blue':1,'fame':0,
        'art.cards':'True','art.enemies':'True'})
    roster=False

def show_units(want):
    global roster
    if roster!=want:
        step('Btn_Roster','查看原部队' if want else '返回手牌');roster=want

def play(n,choice,expected):
    show_units(False);step(f'Hand/Btn_Card_basic_card_{n:03}_0','选择原行动卡')
    step('Inspector/Btn_Choice_'+choice,'选择印刷效果');step('Inspector/Btn_Play','实际出牌',expected)

def sideways(expected):
    show_units(False);step('Hand/Btn_Card_basic_card_000_0','选择原行进');step('Inspector/Btn_Sideways','横置原牌',expected)

def advance(phase,extra=None,capture=False):step('Inspector/Btn_Next','进入_'+phase,{'phase':phase,**(extra or {})},capture)
def target():step('Enemy_0/Btn_Target','选择存活原敌人')

def use(golem,fire,key,power,red):
    global rejects
    show_units(True);step('Units/Btn_Unit_0','查看原卡面',{'ui.unitArtCount':1})
    expected={key:power,'red':red,'units.ready':'False','units.wounds':0}
    step('Inspector/Btn_UnitAbility_'+str(int(fire)),('魔像' if golem else '枪手')+'_'+('火焰格挡' if fire else key),expected,True)
    rejects+=1
    step('Inspector/Btn_UnitAbility_'+str(int(fire)),'重复使用不增加资源',{**expected,'rejections':rejects})

for golem,mode in [(True,'attack'),(True,'block'),(True,'fire'),(False,'attack'),(False,'block')]:
    fire=mode=='fire';block=mode!='attack'
    start(golem,fire);advance('Block')
    red=2 if golem else 1;blue=1
    if block:
        if fire:red-=1
        use(golem,fire,'block',4 if fire else 2 if golem else 5,red)
        if golem and not fire:play(16,'block',{'block':4})
    else:play(16,'strong',{'block':5,'blue':0});blue=0
    effective=4 if golem and block else 5
    need=2 if fire else 4
    target();step('Inspector/Btn_Resolve','实际格挡生效',{'block':0,
        'ui.Feedback':f'格挡成立：有效 {effective} ≥ {need}。效果随承伤阶段结算。'})
    advance('AssignDamage');advance('Melee',{'wounds':0,'realWounds':0})
    if block:
        red-=1;play(21,'strong',{'melee':4,'red':red})
        if fire:
            blue-=1;play(11,'strong',{'ranged':3,'blue':blue});sideways({'melee':5})
    else:
        use(golem,False,'melee',2 if golem else 5,red)
        if golem:sideways({'melee':3})
    power=4 if block else 3 if golem else 5
    fame=6 if fire else 2
    target();step('Inspector/Btn_Resolve','真实击杀和名望',{'kills':1,'fame':fame,
        'ui.Feedback':f'攻击结算：有效 {power} / 护甲 {4 if fire else 3}\n击败 1，名望 +{fame}。'})
    advance('End',{'won':'True','fame':fame,'red':red,'blue':blue,'units.ready':'False','units.wounds':0,'wounds':0},True)

request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
