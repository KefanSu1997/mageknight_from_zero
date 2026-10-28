"""Original Altem Mages: chosen mana, paid cold-fire and battle attack transformations."""
import json,sys
from pathlib import Path
steps=[]
loadout=encounter=rejects=0
roster=False

def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原法师费用、真实贡献、实际承伤与战斗期限') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def show(want):
    global roster
    if roster!=want:step('Btn_Roster','打开原部队' if want else '返回原手牌');roster=want
def unit(i):show(True);step('Units/Btn_Unit_'+str(i),'选择实际部队',{'ui.unitArtCount':2})
def play(n,expected,mode='strong'):
    show(False);step(f'Hand/Btn_Card_basic_card_{n:03}_0','选择原行动卡')
    step('Inspector/Btn_Choice_'+mode,'选择原效果');step('Inspector/Btn_Play','真实出牌付费',expected)
def advance(phase,extra=None,capture=False):step('Inspector/Btn_Next','进入_'+phase,{'phase':phase,**(extra or {})},capture)
def start(name,e=6,supply=False):
    global loadout,encounter,roster
    target=51 if supply else 50
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','出战_'+name))
    while loadout!=target:
        loadout=(loadout+1)%52;step('CampShade/Camp/Btn_Loadout','切换原部队快照',{'draftLoadout':loadout})
    while encounter!=e:
        encounter=(encounter+1)%13;step('CampShade/Camp/Btn_Encounter','切换原敌人',{'draftEncounter':encounter})
    step('CampShade/Camp/Btn_Start','开始_'+name,{'phase':'Ranged','source.units':'army_001,'+('army_001' if supply else 'army_2_014'),
        'units.ready':'True,True','units.wounds':'0,0','red':0 if supply else 2,'blue':0 if supply else 2,
        'tokens.black':0 if supply else 1,'hand':4,'fame':8,'attacks.siege':'False','attacks.coldFire':'False'})
    roster=False
def absorb():
    advance('Block');advance('AssignDamage');step('Enemy_0/Btn_Target','选择原元素攻击');unit(0)
    step('Inspector/Btn_AssignUnit','法师按原火冰抗性承伤')
    advance('Melee',{'units.wounds':'0,0','wounds':0,'realWounds':0,'units.ready':'True,True'},True)
def finish(fame=13):
    advance('End',{'won':'True','fame':fame,'wounds':0,'realWounds':0,'units.wounds':'0,0','attacks.coldFire':'False','attacks.siege':'False'},True)

if '--gold-black-visual-only' not in sys.argv:
    for block in (False,True):
        for boost in range(4):
            e=10 if block and boost==3 else 7 if boost%2 else 6
            name=('block' if block else 'attack')+str(boost)
            start(name,e)
            if block:advance('Block')
            else:absorb()
            unit(0);step('Inspector/Btn_UnitMode_1','选择原冰火攻挡')
            for b in range(boost):step('Inspector/Btn_UnitBoost','选择自付红蓝费用',{'unit.boost':b+1})
            power=9 if boost==3 else 5 if boost==0 else 7
            step('Inspector/Btn_UnitAbility_1',name+'_确认原能力',{'block' if block else 'melee':power,
                'red':2-(boost&1),'blue':2-((boost>>1)&1),'units.ready':'False,True'},True)
            if block:
                step('Enemy_0/Btn_Target','选择实际格挡对象')
                step('Inspector/Btn_Resolve','冰火格挡真实元素与迅捷数值',{'block':0,'ui.Feedback':f'格挡成立：有效 {power} ≥ {8 if e==10 else 2 if e==7 else 3}。效果随承伤阶段结算。'})
                advance('AssignDamage');advance('Melee',{'wounds':0,'units.wounds':'0,0'})
                unit(1);step('Inspector/Btn_UnitAbility_0','剑士原攻击3',{'melee':3})
                play(21,{'melee':7,'red':1-(boost&1)});play(16,{'melee':9},'attack')
            step('Enemy_0/Btn_Target','选择实际击杀目标')
            step('Inspector/Btn_Resolve',name+'_结算',{'kills':1,'fame':11 if e==10 else 13,
                'ui.Feedback':f'攻击结算：有效 {9 if e==10 else 4 if block else power} / 护甲 4\n击败 1，名望 +{3 if e==10 else 5}。'})
            finish(11 if e==10 else 13)

    start('coldfire_conversion');absorb();play(11,{'ranged':3,'blue':1})
    unit(0);step('Inspector/Btn_UnitMode_2','选择原战斗转换')
    step('Inspector/Btn_UnitAbility_2','黑费转换_已有与后续攻击冰火',{'tokens.black':0,'attacks.coldFire':'True','attacks.siege':'False','ranged':3,'units.ready':'False,True'},True)
    play(21,{'melee':4,'red':1,'attacks.coldFire':'True'})
    step('Enemy_0/Btn_Target','选择物抗火抗原敌人')
    step('Inspector/Btn_Resolve','冰火穿抗并保留利斧奖励',{'kills':1,'fame':14,'melee':0,'ranged':0,
        'ui.Feedback':'攻击结算：有效 7 / 护甲 4\n击败 1，名望 +6。'},True)
    finish(14)

    start('siege_conversion',10);play(11,{'ranged':3,'blue':1})
    step('Enemy_0/Btn_Target','原城防阻挡远程');rejects+=1
    step('Inspector/Btn_Resolve','转换前城防明确拒绝',{'rejections':rejects,'kills':0,'ranged':3})
    unit(0);step('Inspector/Btn_UnitMode_2','选择原战斗转换');step('Inspector/Btn_UnitConversion','选择全部攻城',{'unit.conversion':1})
    step('Inspector/Btn_UnitAbility_2','黑费转换_原远程变攻城',{'attacks.siege':'True','attacks.coldFire':'False','siege':3,'ranged':0,'tokens.black':0},True)
    unit(1);step('Inspector/Btn_UnitAbility_0','剑士攻击在远程阶段转攻城',{'siege':6,'melee':0})
    play(16,{'siege':8,'melee':0},'attack');show(False);step('Hand/Btn_Card_basic_card_000_0','选择真实行军横置')
    step('Inspector/Btn_Sideways','实际横置攻击也转攻城',{'siege':9,'melee':0,'played':3,'hand':1})
    step('Enemy_0/Btn_Target','选择原城防目标')
    step('Inspector/Btn_Resolve','攻城穿城防并实际消费所有贡献',{'kills':1,'siege':0,'fame':12,
        'ui.Feedback':'攻击结算：有效 9 / 护甲 4\n击败 1，名望 +4。'},True)
    advance('Block');advance('AssignDamage');advance('Melee',{'wounds':0});finish(12)

    start('red_blue_tokens',0,True);unit(0)
    step('Inspector/Btn_UnitAbility_0','分别选择红蓝魔力而非晶体',{'tokens.red':1,'tokens.blue':1,'red':0,'blue':0,'units.ready':'False,True'},True)
    rejects+=1;step('Inspector/Btn_UnitAbility_0','耗竭拒绝重复产魔力',{'rejections':rejects,'tokens.red':1,'tokens.blue':1})
    advance('Block');play(16,{'block':5,'tokens.blue':0});step('Enemy_0/Btn_Target','实际格挡对象');step('Inspector/Btn_Resolve','消费蓝魔力后的原格挡',{'block':0})
    advance('AssignDamage');advance('Melee',{'wounds':0});play(21,{'melee':4,'tokens.red':0})
    step('Enemy_0/Btn_Target','实际攻击对象');step('Inspector/Btn_Resolve','消费红魔力后击杀',{'kills':1,'fame':10});finish(10)

start('gold_black_tokens',10,True);unit(1);step('Inspector/Btn_UnitMode_2','选择需要黑费的转换');step('Inspector/Btn_UnitConversion','选择攻城')
rejects+=1;step('Inspector/Btn_UnitAbility_2','缺黑魔力不耗竭不转换',{'rejections':rejects,'attacks.siege':'False','units.ready':'True,True','tokens.black':0},True)
unit(0);step('Inspector/Btn_UnitMode_0','原任意魔力包括金黑')
for _ in range(4):step('Inspector/Btn_UnitManaFirst','第一枚选金')
for _ in range(4):step('Inspector/Btn_UnitManaSecond','第二枚选黑')
step('Inspector/Btn_UnitAbility_0','生成金黑真实魔力',{'tokens.gold':1,'tokens.black':1,'unit.manaFirst':'Gold','unit.manaSecond':'Black','ui.unitManaChoices':'第一枚：金色魔力,第二枚：黑色魔力','red':0,'blue':0},True)
unit(1);step('Inspector/Btn_UnitMode_2','第二法师消耗同伴实际黑魔力')
step('Inspector/Btn_UnitAbility_2','白天黑费攻城转换',{'tokens.black':0,'tokens.gold':1,'attacks.siege':'True','units.ready':'False,False'},True)
play(16,{'siege':2},'attack');play(21,{'siege':4},'attack')
step('Enemy_0/Btn_Target','选择原城防目标');step('Inspector/Btn_Resolve','原基础攻击转攻城击杀',{'kills':1,'fame':11,'siege':0,'tokens.gold':1})
advance('Block');advance('AssignDamage');advance('Melee');finish(11)

request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=480,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
