"""Altem Guardians: printed attack/block, Swift and paid all-unit resistance."""
import json
from pathlib import Path
steps=[]
loadout=encounter=rejects=0
roster=False
def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原阿尔蒂姆守护者攻挡、迅捷、绿色费用与所有部队全抗性实际承伤') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def show(want):
    global roster
    if roster!=want:step('Btn_Roster','打开原部队' if want else '返回原手牌');roster=want
def unit(i):show(True);step('Units/Btn_Unit_'+str(i),'选择自有原部队',{'ui.unitArtCount':2})
def play(n,expected,mode='strong'):
    show(False);step(f'Hand/Btn_Card_basic_card_{n:03}_0','选择原行动卡')
    step('Inspector/Btn_Choice_'+mode,'选择原效果');step('Inspector/Btn_Play','实际出牌与付费',expected)
def advance(phase,extra=None,capture=False):step('Inspector/Btn_Next','进入_'+phase,{'phase':phase,**(extra or {})},capture)
for mode,e in [('attack',0),('block',0),('swift',10),('physical',0),('fire',6),('ice',7),('self',0)]:
    buff=mode not in ('attack','block','swift');swift=mode=='swift';selfunit=mode=='self';element=mode in ('fire','ice')
    fame=13 if element else 11 if swift else 10
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','选择守护者出战'))
    while loadout!=49:
        loadout=(loadout+1)%50;step('CampShade/Camp/Btn_Loadout','切换原部队快照',{'draftLoadout':loadout})
    while encounter!=e:
        encounter=(encounter+1)%13;step('CampShade/Camp/Btn_Encounter','切换原敌人',{'draftEncounter':encounter})
    step('CampShade/Camp/Btn_Start','开始原守护者战斗',{'phase':'Ranged','source.units':'army_000,army_2_014',
        'units.ready':'True,True','units.wounds':'0,0','units.resistAll':'False','green':1,'hand':4,'fame':8})
    roster=False;advance('Block')
    if not buff:
        if mode=='attack':play(16,{'block':5,'blue':0})
        else:unit(0);step('Inspector/Btn_UnitAbility_1','原格挡8',{'block':8,'units.ready':'False,True'},True)
        power=5 if mode=='attack' else 16 if swift else 8
        step('Enemy_0/Btn_Target','选择原敌人攻击')
        step('Inspector/Btn_Resolve','实际格挡含迅捷倍率',{'block':0,'ui.Feedback':f'格挡成立：有效 {power} ≥ {8 if swift else 4}。效果随承伤阶段结算。'},swift)
    advance('AssignDamage')
    if buff:
        unit(0);step('Inspector/Btn_UnitAbility_2','支付绿魔力获得本回合全抗性',{'green':0,'units.resistAll':'True','units.ready':'False,True'},mode=='physical')
        rejects+=1;step('Inspector/Btn_UnitAbility_2','耗竭后不能再次发动',{'rejections':rejects,'green':0,'units.resistAll':'True'})
        step('Enemy_0/Btn_Target','指定实际攻击');unit(0 if selfunit else 1)
        step('Inspector/Btn_AssignUnit','实际部队承伤顺序')
    advance('Melee',{'wounds':0,'realWounds':0,'units.wounds':'0,0','units.resistAll':str(buff)},buff)
    if mode=='attack':unit(0);step('Inspector/Btn_UnitAbility_0','原攻击5',{'melee':5,'units.ready':'False,True'},True)
    elif buff:
        unit(1);step('Inspector/Btn_UnitAbility_0','未受伤剑士可实际攻击',{'melee':3,'units.ready':'False,False'})
        play(21,{'melee':7,'red':0});play(16,{'melee':9},'attack')
    else:play(21,{'melee':4,'red':0})
    step('Enemy_0/Btn_Target','选择真实击杀目标')
    power=5 if mode=='attack' else 4 if not buff or element else 9
    step('Inspector/Btn_Resolve','实际攻击消费与击杀',{'kills':1,'fame':fame,
        'ui.Feedback':f'攻击结算：有效 {power} / 护甲 {4 if element or swift else 3}\n击败 1，名望 +{fame-8}。'})
    advance('End',{'won':'True','fame':fame,'wounds':0,'realWounds':0,'units.wounds':'0,0',
        'units.ready':'False,False' if buff else 'False,True','units.resistAll':str(buff)},True)
request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=360,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
