"""Original Bow of Starsdawn: real material identities, phase rewards and per-attack choices."""
import json,sys
from pathlib import Path
steps=[]
loadout=encounter=rejects=0
roster=False
def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='星辰之弓原弃牌、逐笔同元素互斥选择与阶段/回合期限') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def show(want):
    global roster
    if roster!=want:step('Btn_Roster','打开部队' if want else '返回手牌');roster=want
def unit(ability,expected):
    show(True);step('Units/Btn_Unit_0','选择原部队',{'ui.unitArtCount':1});step('Inspector/Btn_UnitAbility_'+str(ability),'实际原部队攻击与费用',expected)
def play(n,expected):
    show(False);step(f'Hand/Btn_Card_basic_card_{n:03}_0','选择原行动卡');step('Inspector/Btn_Choice_strong','选择原强化');step('Inspector/Btn_Play','实际出牌',expected)
def advance(phase,extra=None,capture=False):step('Inspector/Btn_Next','进入_'+phase,{'phase':phase,**(extra or {})},capture)
def start(name,e=2,siege=False):
    global loadout,encounter,roster
    target=53 if siege else 52
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','出战_'+name))
    while loadout!=target:
        loadout=(loadout+1)%54;step('CampShade/Camp/Btn_Loadout','切换原神器出战',{'draftLoadout':loadout})
    while encounter!=e:
        encounter=(encounter+1)%13;step('CampShade/Camp/Btn_Encounter','切换原敌群',{'draftEncounter':encounter})
    step('CampShade/Camp/Btn_Start','开始_'+name,{'phase':'Ranged','source.artifacts':'items_016','source.units':'army_004' if siege else 'army_003',
        'hand':6,'fame':3,'bow.active':'False','bow.fame':0,'units.ready':'True'})
    roster=False
def bow(burn=False,materials=False):
    step('Hand/Btn_Card_items_016_0','选择原星辰之弓',{'ui.cardArtCount':6,'art.cards':'True'})
    if burn:step('Inspector/Btn_ArtifactBurn','原焚毁效果')
    if materials:
        step('Inspector/Btn_ArtifactMaterials','选择实际弃牌')
        step('Hand/Btn_Card_basic_card_016_0','选择真实决心实例');step('Hand/Btn_Card_basic_card_000_0','选择真实行军实例')
        step('Inspector/Btn_ArtifactMaterials','完成材料选择',{'material.ids':'basic_card_016,basic_card_000'})
    step('Inspector/Btn_ArtifactPlay','实际焚毁' if burn else '真实材料弃置并使用',{'bow.active':str(burn),'bow.fame':0 if burn else 1,
        'removed.ids':'items_016' if burn else '', 'played.ids':'' if burn else 'items_016',
        'discard.ids':'basic_card_000,basic_card_016' if materials else '', 'ranged':4 if materials else 0,
        'hand':3 if materials else 5},not burn)
def attack(i,power,armor,delta,fame,label='实际击杀',capture=False):
    step(f'Enemy_{i}/Btn_Target','选择实际敌人')
    step('Inspector/Btn_Resolve',label,{'fame':fame,'ui.Feedback':f'攻击结算：有效 {power} / 护甲 {armor}\n击败 1，名望 +{delta}。'},capture)
def end(fame,burn=False):
    advance('End',{'won':'True','fame':fame,'wounds':0,'realWounds':0,'bow.fame':0,'bow.active':str(burn),'units.wounds':'0'},True)
def close_phases():advance('Block');advance('AssignDamage');advance('Melee',{'wounds':0})
def panel():step('Btn_BowChoices','打开或收起逐笔选择')

start('normal_materials');bow(materials=True);attack(0,4,3,3,6)
unit(1,{'ranged':6,'red':1,'units.ready':'False'});attack(1,6,3,3,9,'同阶段后续攻击仍加名望',True);close_phases();end(9)

start('normal_zero');bow();unit(1,{'ranged':6,'red':1});attack(0,6,3,3,6)
advance('Block',{'bow.fame':0});play(16,{'block':5,'blue':0});step('Enemy_1/Btn_Target','格挡剩余原敌人');step('Inspector/Btn_Resolve','实际格挡',{'block':0})
advance('AssignDamage');advance('Melee',{'wounds':0});play(21,{'melee':4,'red':0});attack(1,4,3,2,8,'跨阶段不再附加名望',True);end(8)

start('burn_ranged_double',7);bow(True);unit(1,{'ranged':12,'red':1});panel()
step('Inspector/Btn_BowDouble','原火焰远程6翻倍',{'ui.bowPower':'当前：火远程12','bow.choices':'1','ranged':12},True)
panel();attack(0,12,4,5,8);close_phases();end(8,True)

start('burn_ranged_siege',10);bow(True);play(7,{'ranged':6,'white':0});play(11,{'ranged':12,'blue':0});panel()
step('Inspector/Btn_BowSiege','只将第一笔转攻城',{'bow.choices':'2,0','siege':3,'ranged':6,'ui.bowPower':'当前：物理攻城3'},True)
panel();step('Enemy_0/Btn_Target','城防目标');rejects+=1
step('Inspector/Btn_Resolve','仍含远程所以被城防拒绝',{'rejections':rejects,'kills':0,'siege':3,'ranged':6})
panel();step('Inspector/Btn_BowContribution','选择第二笔原卡攻击')
step('Inspector/Btn_BowSiege','第二笔独立转攻城',{'bow.choices':'2,2','siege':6,'ranged':0,'ui.bowPower':'当前：物理攻城3'},True)
panel();attack(0,6,4,4,7,'实际攻城并保留原利斧名望');close_phases();end(7,True)

start('burn_siege_keep',10,True);bow(True);unit(1,{'siege':5,'ranged':0,'red':0});panel()
step('Inspector/Btn_BowDouble','攻城翻倍必须转远程',{'siege':0,'ranged':10,'ui.bowPower':'当前：火远程10'},True)
panel();step('Enemy_0/Btn_Target','原城防目标');rejects+=1
step('Inspector/Btn_Resolve','翻倍后远程被城防拒绝',{'rejections':rejects,'kills':0,'ranged':10})
panel();step('Inspector/Btn_BowSiege','保留原攻城不得同时翻倍',{'siege':5,'ranged':0,'ui.bowPower':'当前：火攻城5'},True)
panel();attack(0,5,4,3,6);close_phases();end(6,True)

start('burn_siege_double',6,True);bow(True);unit(2,{'siege':5,'ranged':0,'blue':0});panel()
step('Inspector/Btn_BowDouble','寒冰攻城翻倍保持冰元素',{'siege':0,'ranged':10,'ui.bowPower':'当前：冰远程10'},True)
panel();attack(0,10,4,5,8);close_phases();end(8,True)

if '--sword-display-only' in sys.argv:
    steps.clear();loadout=encounter=rejects=0;roster=False
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','直接受影响的公正之剑显示回归'))
    for i in range(5):step('CampShade/Camp/Btn_Loadout','选择既有公正之剑快照',{'draftLoadout':i+1})
    for i in range(3):step('CampShade/Camp/Btn_Encounter','选择既有毒素物抗敌人',{'draftEncounter':i+1})
    step('CampShade/Camp/Btn_Start','开始既有焚毁场景',{'phase':'Ranged','fame':0,'source.artifacts':'items_008','hand':4})
    advance('Block');play(16,{'block':5,'blue':0});step('Enemy_0/Btn_Target','正式格挡对象');step('Inspector/Btn_Resolve','决心原格挡',{'block':0})
    advance('AssignDamage');advance('Melee',{'wounds':0})
    step('Hand/Btn_Card_items_008_0','选择公正之剑');step('Inspector/Btn_ArtifactBurn','选择原焚毁')
    step('Inspector/Btn_ArtifactPlay','真实神器永久移除',{'removed.ids':'items_008','melee':0,'hand':2})
    step('Hand/Btn_Card_basic_card_021_0','选择原狂怒');step('Inspector/Btn_Choice_attack','原基础攻击2')
    step('Inspector/Btn_Play','统一显示翻倍后攻击4',{'melee':4,'red':1,'hand':1,'bow.active':'False'},True)
    attack(0,4,3,5,5,'实际攻击与显示4一致',True)
    advance('End',{'won':'True','wounds':0,'fame':5,'removed.ids':'items_008'},True)

request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=360,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
