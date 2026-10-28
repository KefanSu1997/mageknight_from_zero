"""Focused guard/swordsmen original combat abilities; seven independent real UI battles."""
import json
from pathlib import Path

steps=[]
loadout=encounter=0
roster=False
rejects=0
def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='乌顿原能力、迅捷专用翻倍、部队创伤与耗竭及实际战斗') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def start(l,e,unit):
    global loadout,encounter,roster
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','选择独立部队战斗'))
    while loadout!=l:
        loadout=(loadout+1)%10
        step('CampShade/Camp/Btn_Loadout','选择出战部队',{'draftLoadout':loadout})
    while encounter!=e:
        encounter=(encounter+1)%5
        step('CampShade/Camp/Btn_Encounter','选择原敌人',{'draftEncounter':encounter})
    step('CampShade/Camp/Btn_Start','建立独立快照',{'phase':'Ranged','source.units':unit,'units.ready':'True','units.wounds':0,'hand':3,'fame':0})
    roster=False
def show_units(want):
    global roster
    if roster!=want:
        step('Btn_Roster','查看部队原图' if want else '返回实际手牌')
        roster=want
def unit():
    show_units(True)
    step('Units/Btn_Unit_0','选择原部队',{'ui.unitArtCount':1})
def card(n):
    show_units(False)
    step(f'Hand/Btn_Card_basic_card_{n:03}_0','选择实际原卡')
def play(n,strong=False):
    card(n)
    if strong:step('Inspector/Btn_Choice_strong','选择强化原卡')
    step('Inspector/Btn_Play','原卡正式出牌')
def advance(phase,extra=None,capture=False):step('Inspector/Btn_Next','进入_'+phase,{'phase':phase,**(extra or {})},capture)
def target():step('Enemy_0/Btn_Target','选择原敌人')
def block(effective,need):
    target()
    step('Inspector/Btn_Resolve','完成真实格挡',{'block':0,'ui.Feedback':f'格挡成立：有效 {effective} ≥ {need}。效果随承伤阶段结算。'})

cases=[(8,0,False,True,'守卫普通敌人格挡'),(8,4,False,True,'守卫迅捷翻倍格挡'),(8,0,False,False,'守卫攻击'),
       (9,0,False,True,'剑士普通格挡'),(9,4,True,True,'剑士自伤格挡'),(9,0,False,False,'剑士普通攻击'),(9,3,True,False,'剑士自伤攻击')]
for l,e,strong,isblock,label in cases:
    start(l,e,'army_2_013' if l==8 else 'army_2_014')
    advance('Block')
    if not isblock:
        play(16,True);block(5,4);advance('AssignDamage');advance('Melee',{'wounds':0})
    unit()
    ability=(1 if isblock else 0) if l==8 else int(strong)
    value=(4 if isblock else 2) if l==8 else (6 if strong else 3)
    wound=int(l==9 and strong)
    step('Inspector/Btn_UnitAbility_'+str(ability),label,{'block' if isblock else 'melee':value,'units.ready':'False','units.wounds':wound},True)
    if l==9:
        rejects+=1
        step('Inspector/Btn_UnitAbility_'+str(ability),'重复发动不得重复获得数值或伤牌',{'block' if isblock else 'melee':value,'units.wounds':wound,'rejections':rejects})
    if isblock:
        if l==9 and not strong:
            card(0);step('Inspector/Btn_Sideways','真实横置补足格挡',{'block':4})
        target()
        effective=8 if l==8 and e==4 else 6 if strong else 4
        need=6 if e==4 else 4
        step('Btn_Menu','打开战斗菜单')
        step('Btn_PhaseRules','格挡需求及贡献核对',{'ui.TargetPreview':f'攻击 #1   格挡需求 {need}\n当前有效格挡 {effective}（不足时不能减伤）'},l==8)
        step('Inspector/Btn_Resolve','实际格挡结算',{'block':0})
        advance('AssignDamage');advance('Melee',{'wounds':0,'realWounds':0,'units.wounds':wound})
        play(21,True)
    elif l==8:
        card(0);step('Inspector/Btn_Sideways','守卫攻击配合真实横置',{'melee':3})
    target()
    fame=3 if e==4 else 4 if e==3 else 2
    effective=4 if isblock else 3
    armor=4 if e==4 else 3
    step('Inspector/Btn_Resolve','实际击杀与名望',{'kills':1,'fame':fame,
        'ui.Feedback':f'攻击结算：有效 {effective} / 护甲 {armor}\n击败 1，名望 +{fame}。'})
    advance('End',{'won':'True','fame':fame,'units.wounds':wound,'units.ready':'False','wounds':0,'discardWounds':0},True)

request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
