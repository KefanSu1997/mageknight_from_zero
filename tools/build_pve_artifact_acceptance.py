"""Focused original artifact hand/attachment/material/burn interactions in playable battles."""
import json
from pathlib import Path
ROOT = "Canvas/BattleTable/"
steps=[]
def step(path,label,expected=None,capture=False):
    values={"ui.truncated":"",**(expected or {})}
    steps.append(dict(buttonPath=ROOT+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule="原神器普通/焚毁、真实材料与附旗、实际资源和击杀奖励") for k,v in values.items()]))
loadout=0; encounter=0
def start(l,e):
    global loadout,encounter
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','打开出战配置'))
    while loadout!=l:
        loadout=(loadout+1)%6
        step('CampShade/Camp/Btn_Loadout','选择出战快照',{'draftLoadout':loadout})
    while encounter!=e:
        encounter=(encounter+1)%4
        step('CampShade/Camp/Btn_Encounter','选择遭遇',{'draftEncounter':encounter})
    step('CampShade/Camp/Btn_Start','开始独立原神器战斗',{'loadout':l,'encounter':e,'phase':'Ranged','hand':4,'fame':0,'removed.ids':'','discard.ids':'','ui.cardArtCount':4})
def card(id):step('Hand/Btn_Card_'+id+'_0','选择原卡_'+id)
def target(i):step(f'Enemy_{i}/Btn_Target','选择原敌人_'+str(i))
def next(phase,values=None,capture=False):step('Inspector/Btn_Next','推进_'+phase,{'phase':phase,**(values or {})},capture)
def attack(i,values):
    target(i);step('Inspector/Btn_Resolve','实际攻击结算',values)
def play(id,choice=None,values=None):
    card(id)
    if choice:step('Inspector/Btn_Choice_'+choice,'选择原效果')
    step('Inspector/Btn_Play','确认原卡',values)

for burn in [False,True]:
    start(4,2);card('items_000')
    if burn:step('Inspector/Btn_ArtifactBurn','选择荣誉焚毁')
    else:step('Inspector/Btn_ArtifactUnit','确认附着魔像')
    step('Inspector/Btn_ArtifactPlay','荣誉旌旗实际去向',{'hand':3,'played':0,'units.banners':'none' if burn else 'items_000',
        'units.armor':4,'removed.ids':'items_000' if burn else ''},True)
    next('Block');step('Btn_Roster','查看附旗部队');step('Units/Btn_Unit_0','选择原魔像')
    step('Inspector/Btn_UnitAbility_2','旌旗令寒冰格挡加一并奖励名望',{'block':5,'blue':0,'units.ready':'False','fame':1,'units.armor':4},True)
    target(0);step('Inspector/Btn_Resolve','挡住第一名敌人',{'block':0,'fame':1})
    next('AssignDamage');target(1);step('Units/Btn_Unit_0','选择魔像承担第二击')
    step('Inspector/Btn_AssignUnit','预留实际部队',{'units.plan':'1:0:army_2_001','units.wounds':0})
    next('Melee',{'units.wounds':0,'wounds':0,'realWounds':0,'fame':1},True)
    step('Btn_Roster','返回手牌')
    play('basic_card_021','strong',{'melee':4,'red':0});attack(1,{'kills':1,'fame':3})
    play('basic_card_016',values={'melee':2});card('basic_card_000')
    step('Inspector/Btn_Sideways','横置原卡补足',{'melee':3});attack(0,{'kills':2,'fame':5})
    next('End',{'won':'True','fame':5,'wounds':0,'units.wounds':0,'removed.ids':'items_000' if burn else '',
        'units.banners':'none' if burn else 'items_000'},True)

for burn in [False,True]:
    start(5,3 if burn else 0)
    next('Block');play('basic_card_016','strong',{'blue':0,'block':5});target(0)
    step('Inspector/Btn_Resolve','决心正式格挡',{'block':0})
    next('AssignDamage');next('Melee',{'wounds':0})
    card('items_008')
    if burn:step('Inspector/Btn_ArtifactBurn','选择正义之剑焚毁')
    else:
        step('Inspector/Btn_ArtifactMaterials','进入实际手牌材料选择')
        card('basic_card_000')
        step('Inspector/Btn_ArtifactMaterials','确认所选真实材料',{'material.ids':'basic_card_000','hand':3},True)
    step('Inspector/Btn_ArtifactPlay','正义之剑普通或焚毁去向',{'hand':2 if burn else 1,'played':1 if burn else 2,
        'discard.ids':'' if burn else 'basic_card_000','removed.ids':'items_008' if burn else '',
        'melee':0 if burn else 3,'material.ids':''},True)
    if burn:play('basic_card_021',values={'melee':4,'red':1})
    attack(0,{'kills':1,'fame':5 if burn else 3,
        'ui.Feedback':f'攻击结算：有效 {4 if burn else 3} / 护甲 3\n击败 1，名望 +{5 if burn else 3}。'})
    next('End',{'won':'True','wounds':0,'fame':5 if burn else 3,'removed.ids':'items_008' if burn else '',
        'discard.ids':'' if burn else 'basic_card_000'},True)

request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=180,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
