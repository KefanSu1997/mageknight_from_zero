"""Original Fortitude Banner, owned unit protection and postcombat full healing."""
import json
from pathlib import Path
steps=[]
loadout=encounter=rejects=0
roster=False
def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原强韧旌旗：真实附着/一次创伤/战外治疗/物理牌守恒') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def start(e):
    global loadout,encounter,roster
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','选择原强韧旌旗'))
    while loadout!=28:
        loadout=(loadout+1)%29;step('CampShade/Camp/Btn_Loadout','选择出战快照',{'draftLoadout':loadout})
    while encounter!=e:
        encounter=(encounter+1)%13;step('CampShade/Camp/Btn_Encounter','选择原敌人',{'draftEncounter':encounter})
    step('CampShade/Camp/Btn_Start','开始实际战斗',{'phase':'Ranged','hand':5,'fame':8,'units.ready':'True,True','units.wounds':'0,0','units.banners':'none,none','source.artifacts':'items_018'})
    roster=False
def show(want):
    global roster
    if roster!=want:step('Btn_Roster','查看部队' if want else '返回手牌');roster=want
def card(id):show(False);step('Hand/Btn_Card_'+id+'_0','选择原卡_'+id)
def unit(i=0):show(True);step('Units/Btn_Unit_'+str(i),'选择实际部队_'+str(i),{'ui.unitArtCount':2})
def play(n,choice,expected=None):
    card(f'basic_card_{n:03}');step('Inspector/Btn_Choice_'+choice,'选择原效果');step('Inspector/Btn_Play','正式出牌',expected)
def advance(p,expected=None,capture=False):step('Inspector/Btn_Next','进入_'+p,{'phase':p,**(expected or {})},capture)
def target():step('Enemy_0/Btn_Target','选择实际敌人')
def attach():
    card('items_018');step('Inspector/Btn_ArtifactPlay','原强韧旌旗附着第一支部队',{'hand':4,'played':0,'units.banners':'items_018,none'},True)
def blockedmelee():
    advance('Block');play(16,'strong',{'block':5,'blue':1});target();step('Inspector/Btn_Resolve','决心完整格挡',{'block':0})
    advance('AssignDamage');advance('Melee',{'wounds':0,'realWounds':0})
def attack(power,armor,reward):
    target();step('Inspector/Btn_Resolve','正式击杀',{'kills':1,'fame':8+reward,'ui.Feedback':f'攻击结算：有效 {power} / 护甲 {armor}\n击败 1，名望 +{reward}。'})
for cancel in (False,True):
    start(0);attach();blockedmelee();unit()
    step('Inspector/Btn_Fortitude','主动选择本轮一次免伤',{'units.fortitudeSelected':'True,False'})
    if cancel:step('Inspector/Btn_Fortitude','取消免伤选择',{'units.fortitudeSelected':'False,False'})
    step('Inspector/Btn_UnitAbility_1','乌顿剑士原能力自伤',{'melee':6,'units.wounds':'1,0' if cancel else '0,0','units.ready':'False,True',
        'units.fortitudeSpent':'False,False' if cancel else 'True,False'},True)
    if not cancel:
        rejects+=1;step('Inspector/Btn_Fortitude','本轮使用后拒绝重复免伤',{'rejections':rejects,'units.fortitudeSpent':'True,False','units.wounds':'0,0'})
    attack(6,3,2);advance('End',{'won':'True','fame':10,'units.wounds':'1,0' if cancel else '0,0','units.fortitudeSelected':'False,False','units.banners':'items_018,none'},True)
start(3);attach();advance('Block');advance('AssignDamage');target();unit()
step('Inspector/Btn_Fortitude','选择忽略毒素创伤',{'units.fortitudeSelected':'True,False'})
step('Inspector/Btn_AssignUnit','安排原部队实际承伤',{'units.plan':'0:0:army_2_014'})
advance('Melee',{'units.wounds':'0,0','units.fortitudeSpent':'True,False','units.ready':'True,True','wounds':0,'realWounds':0},True)
unit();step('Inspector/Btn_UnitAbility_0','免伤后激活原攻击',{'melee':3,'units.ready':'False,True'})
play(21,'strong',{'melee':7,'red':1});attack(3,3,4);advance('End',{'won':'True','fame':12,'units.wounds':'0,0','wounds':0},True)
start(0);card('items_018');step('Inspector/Btn_ArtifactBurn','选择原焚毁')
rejects+=1;step('Inspector/Btn_ArtifactPlay','战斗中拒绝治疗',{'rejections':rejects,'hand':5,'removed.ids':'','units.wounds':'0,0'},True)
blockedmelee()
for i in (0,1):
    unit(i);step('Inspector/Btn_UnitAbility_1','两支真实部队分别自伤',{'units.wounds':'1,0' if i==0 else '1,1','units.ready':'False,True' if i==0 else 'False,False','melee':6 if i==0 else 12},i==1)
attack(12,3,2);advance('End',{'won':'True','units.wounds':'1,1','units.ready':'False,False','fame':10},True)
card('items_018');step('Inspector/Btn_ArtifactBurn','战后选择焚毁完全治疗')
step('Inspector/Btn_ArtifactPlay','原神器永久移除并完全治疗',{'removed.ids':'items_018','units.wounds':'0,0','units.ready':'False,False','wounds':0,'fame':10},True)
show(True);step('Units/Btn_Unit_0','核对治疗不重整',{'units.wounds':'0,0','units.ready':'False,False'},True)
request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
