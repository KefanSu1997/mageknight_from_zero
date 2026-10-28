"""Audited Fire/Ice Mages: all printed choices and actual mana/resistance consumers."""
import json,sys
from pathlib import Path
steps=[];loadout=encounter=rejects=0

def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原火冰法师：实际元素、费用、耗竭、资源消费、抗性承伤和战斗结算') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def start(l,e,fire):
    global loadout,encounter
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','选择原法师出战'))
    while loadout!=l:
        loadout=(loadout+1)%38;step('CampShade/Camp/Btn_Loadout','切换原部队快照',{'draftLoadout':loadout})
    while encounter!=e:
        encounter=(encounter+1)%13;step('CampShade/Camp/Btn_Encounter','切换原敌人',{'draftEncounter':encounter})
    step('CampShade/Camp/Btn_Start','开始实际法师战斗',{'phase':'Ranged','fame':0,'hand':4,'units.ready':'True','units.wounds':0,
        'source.units':'army_007' if fire else 'army_013','units.armor':4,'red':0 if l==36 else 1 if l==37 else 2,
        'blue':1 if l==36 else 0 if l==37 else 2,'art.cards':'True','art.enemies':'True','tokens.red':0,'tokens.blue':0})
def advance(p,expected=None,capture=False):step('Inspector/Btn_Next','进入_'+p,{'phase':p,**(expected or {})},capture)
def play(n,expected=None):
    step(f'Hand/Btn_Card_basic_card_{n:03}_0','选择原行动卡');step('Inspector/Btn_Choice_strong','选择原卡强化')
    step('Inspector/Btn_Play','消费实际魔力发动原卡',expected)
def unit(a,expected,capture=True,repeat=False):
    global rejects
    step('Btn_Roster','打开真实部队');step('Units/Btn_Unit_0','选择原法师牌面',{'ui.unitArtCount':1})
    step(f'Inspector/Btn_UnitAbility_{a}','发动原部队能力',{'units.ready':'False',**expected},capture)
    if repeat:
        rejects+=1;step(f'Inspector/Btn_UnitAbility_{a}','耗竭部队拒绝再次生成魔力',{'rejections':rejects,'ui.Feedback':'部队必须就绪、未受伤且未销毁',**expected})
    step('Btn_Roster','回到原手牌')
def block(power,need):
    step('Enemy_0/Btn_Target','选择真实敌人攻击');step('Inspector/Btn_Resolve','提交实际格挡',{'ui.Feedback':f'格挡成立：有效 {power} ≥ {need}。效果随承伤阶段结算。'})
    advance('AssignDamage');advance('Melee',{'wounds':0,'realWounds':0})
def physical8():
    play(21,{'melee':4});play(11,{'ranged':3})
    step('Hand/Btn_Card_basic_card_000_0','选择原行进横置');step('Inspector/Btn_Sideways','增加实际普通攻击1',{'melee':5,'ranged':3})
def attack(power,armor,fame):
    step('Enemy_0/Btn_Target','选择真实敌人');step('Inspector/Btn_Resolve','结算实际击杀',{'kills':1,'fame':fame,
        'ui.Feedback':f'攻击结算：有效 {power} / 护甲 {armor}\n击败 1，名望 +{fame}。'})
def finish(ranged,fame,red,blue,capture=False):
    if ranged:advance('Block');advance('AssignDamage');advance('Melee')
    advance('End',{'won':'True','fame':fame,'wounds':0,'realWounds':0,'units.wounds':0,'units.ready':'False','red':red,'blue':blue},capture)
def resist(fire):
    advance('Block');advance('AssignDamage')
    step('Enemy_0/Btn_Target','选择真实元素攻击');step('Btn_Roster','打开法师承伤');step('Units/Btn_Unit_0','选择抗性法师')
    step('Inspector/Btn_UnitAbility_2','承伤阶段生成实际魔力',{'units.ready':'False','red':3 if fire else 2,'blue':2 if fire else 3,
        'tokens.red':1 if fire else 0,'tokens.blue':0 if fire else 1},True)
    step('Inspector/Btn_AssignUnit','耗竭法师仍加入承伤顺序',{'units.plan':'0:0:army_007' if fire else '0:0:army_013'})
    advance('Melee',{'wounds':0,'realWounds':0,'units.wounds':0,'units.ready':'False'},True)
    step('Btn_Roster','返回原手牌');physical8();attack(4,4,6);finish(False,6,3 if fire else 1,1 if fire else 3)

if '--labels-only' in sys.argv:
    start(34,6,True)
    step('Btn_Roster','查看普通阶段魔力按钮');step('Units/Btn_Unit_0','核对原法师能力按钮',{'ui.unitArtCount':1},True)
    advance('Block');advance('AssignDamage')
    step('Enemy_0/Btn_Target','选择承伤敌人');step('Units/Btn_Unit_0','核对承伤阶段魔力按钮',{'units.ready':'True'},True)
else:
    start(34,0,True);unit(0,{'ranged':3,'red':2,'blue':2});attack(3,3,2);finish(True,2,2,2,True)
    start(34,3,True);advance('Block');play(16,{'block':5,'blue':1});block(5,4);unit(1,{'melee':6,'red':1});attack(6,3,4);finish(False,4,1,1)
    start(34,6,True);resist(True)
    start(34,7,True);advance('Block');unit(1,{'block':6,'red':1});block(6,2);physical8();attack(4,4,6);finish(False,6,0,1)
    start(35,10,False);unit(1,{'siege':4,'blue':1});attack(4,4,3);finish(True,3,2,1)
    start(35,3,False);advance('Block');play(16,{'block':5,'blue':1});block(5,4);unit(0,{'melee':4,'red':2});attack(4,3,4);finish(False,4,2,1)
    start(35,6,False);advance('Block');unit(0,{'block':4,'blue':2});block(4,3);physical8();attack(4,4,6);finish(False,6,1,1)
    start(35,7,False);resist(False)
    start(36,0,True);unit(2,{'red':1,'tokens.red':1,'blue':1},repeat=True);advance('Block');play(16,{'block':5,'blue':0});block(5,4);play(21,{'melee':4,'tokens.red':0,'red':1});attack(4,3,2);finish(False,2,1,0,True)
    start(37,0,False);unit(2,{'blue':1,'tokens.blue':1,'red':1},repeat=True);play(11,{'ranged':3,'tokens.blue':0,'blue':1});attack(3,3,3);finish(True,3,1,1,True)
request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=480,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
