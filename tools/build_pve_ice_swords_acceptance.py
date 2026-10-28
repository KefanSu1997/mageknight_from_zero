"""Original ice swordsmen: ordinary combat and blue target control, plus immunity rejection."""
import json
from pathlib import Path
steps=[]
loadout=encounter=rejects=0
roster=False
def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原冰霜剑士费用、目标取消、减甲、真实承伤与击杀') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def start(e):
    global loadout,encounter,roster
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','选择原冰霜剑士'))
    while loadout!=27:
        loadout=(loadout+1)%28;step('CampShade/Camp/Btn_Loadout','选择部队快照',{'draftLoadout':loadout})
    while encounter!=e:
        encounter=(encounter+1)%13;step('CampShade/Camp/Btn_Encounter','切换原敌人',{'draftEncounter':encounter})
    step('CampShade/Camp/Btn_Start','开始战斗',{'phase':'Ranged','source.units':'army_002','units.ready':'True','units.wounds':0,'units.armor':6,
        'red':2,'blue':2,'hand':4,'fame':0,'art.cards':'True','art.enemies':'True'})
    roster=False
def show(want):
    global roster
    if roster!=want:step('Btn_Roster','查看部队' if want else '返回手牌');roster=want
def selectunit():
    show(True);step('Units/Btn_Unit_0','选择原部队',{'ui.unitArtCount':1})
def ability(i,expected,capture=False):
    global rejects
    selectunit();step('Inspector/Btn_UnitAbility_'+str(i),'发动原能力',{'units.ready':'False',**expected},capture)
    rejects+=1;step('Inspector/Btn_UnitAbility_'+str(i),'拒绝耗竭后重复发动',{'rejections':rejects,'units.ready':'False',**expected})
def play(n,choice,expected=None):
    show(False);step(f'Hand/Btn_Card_basic_card_{n:03}_0','选择原卡');step('Inspector/Btn_Choice_'+choice,'选择原效果');step('Inspector/Btn_Play','实际出牌',expected)
def advance(p,expected=None,capture=False):step('Inspector/Btn_Next','进入_'+p,{'phase':p,**(expected or {})},capture)
def target():step('Enemy_0/Btn_Target','选择原敌人')
def attack(power,armor,fame):
    target();step('Inspector/Btn_Resolve','实际击杀',{'kills':1,'fame':fame,'ui.Feedback':f'攻击结算：有效 {power} / 护甲 {armor}\n击败 1，名望 +{fame}。'},True)
def end(fame,red,blue):
    advance('End',{'won':'True','fame':fame,'wounds':0,'realWounds':0,'units.wounds':0,'units.ready':'False','red':red,'blue':blue,'enemies.skipped':''},True)
for block in (False,True):
    start(0);advance('Block')
    if block:ability(0,{'block':5,'blue':2},True)
    else:play(16,'strong',{'block':5,'blue':1})
    target();step('Inspector/Btn_Resolve','原格挡5',{'block':0,'ui.Feedback':'格挡成立：有效 5 ≥ 4。效果随承伤阶段结算。'})
    advance('AssignDamage');advance('Melee',{'wounds':0,'realWounds':0})
    if block:play(21,'strong',{'melee':4,'red':1})
    else:ability(0,{'melee':5,'blue':1},True)
    attack(4 if block else 5,3,2);end(2,1 if block else 2,2 if block else 1)
for fire in (False,True):
    start(6 if fire else 0);advance('Block');target()
    ability(1,{'blue':1,'enemies.armor':1,'enemies.skipped':0},True)
    advance('AssignDamage');advance('Melee',{'wounds':0,'realWounds':0,'enemies.armor':1,'enemies.skipped':0},True)
    play(16,'attack',{'melee':2});attack(1 if fire else 2,1,5 if fire else 2);end(5 if fire else 2,2,1)
# Original ice-resistant lich rejects blue control without consuming the unit or crystal.
start(7);advance('Block');target();selectunit();rejects+=1
step('Inspector/Btn_UnitAbility_1','寒冰抗性拒绝控制',{'rejections':rejects,'units.ready':'True','blue':2,'enemies.armor':4,'enemies.skipped':'',
    'ui.Feedback':'冰霜抗性敌人不能成为冰冻目标'},True)
request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
