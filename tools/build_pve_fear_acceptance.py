"""Fear Banner: exact original attack slot, residual unit damage, block+cancel, three-enemy burn."""
import json
from pathlib import Path
steps=[]
loadout=encounter=rejects=0
roster=False
def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='恐惧旌旗普通取消一次攻击，焚毁令所选敌人停攻；真实耗竭、承伤与结算') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def start(e):
    global loadout,encounter,roster
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','选择原恐惧旌旗战斗'))
    while loadout!=15:
        loadout=(loadout+1)%16;step('CampShade/Camp/Btn_Loadout','选择附旗手牌',{'draftLoadout':loadout})
    while encounter!=e:
        encounter=(encounter+1)%10;step('CampShade/Camp/Btn_Encounter','选择多段攻击遭遇',{'draftEncounter':encounter})
    step('CampShade/Camp/Btn_Start','重建真实原卡与部队',{'phase':'Ranged','hand':5,'units.banners':'none','units.ready':'True','fame':0,'attacks.cancelled':'','enemies.skipped':''})
    roster=False
def units(want):
    global roster
    if roster!=want:step('Btn_Roster','查看部队' if want else '返回原手牌');roster=want
def card(id):units(False);step('Hand/Btn_Card_'+id+'_0','选择原卡_'+id)
def play(n,choice,expected=None):
    card(f'basic_card_{n:03}');step('Inspector/Btn_Choice_'+choice,'选择印刷效果');step('Inspector/Btn_Play','正式出牌',expected)
def sideways(expected=None):card('basic_card_000');step('Inspector/Btn_Sideways','横置真实原卡',expected)
def target(i=0):step(f'Enemy_{i}/Btn_Target','选择实际敌人_'+str(i))
def advance(phase,extra=None,capture=False):step('Inspector/Btn_Next','进入_'+phase,{'phase':phase,**(extra or {})},capture)

for blockfirst in (False,True):
    start(8);card('items_001')
    step('Inspector/Btn_ArtifactUnit','附着到原魔像')
    step('Inspector/Btn_ArtifactPlay','真实旗帜附着',{'hand':4,'played':0,'units.banners':'items_001','fame':0},True)
    advance('Block')
    if blockfirst:
        play(16,'strong',{'block':5,'blue':0});sideways({'block':6});target()
        step('Inspector/Btn_Resolve','先格挡火焰攻击第一段',{'block':0,'ui.Feedback':'格挡成立：有效 3 ≥ 3。效果随承伤阶段结算。'})
    units(True);step('Units/Btn_Unit_0','选择附旗魔像')
    step('Inspector/Btn_BannerMode','切换到恐惧旌旗原能力');target()
    if blockfirst:
        rejects+=1;step('Inspector/Btn_UseFear','已格挡攻击不能领取取消奖励',{'rejections':rejects,'units.ready':'True','fame':0})
        step('Inspector/Btn_AttackSlot','改选第二段冰攻击',{'attackSlot':1})
    step('Inspector/Btn_UseFear','取消所选一次攻击',{'attacks.cancelled':'0:1' if blockfirst else '0:0','enemies.skipped':'','units.ready':'False','fame':1},True)
    rejects+=1;step('Inspector/Btn_UseFear','耗竭部队不得重复领取奖励',{'rejections':rejects,'fame':1})
    advance('AssignDamage')
    if not blockfirst:
        target();step('Units/Btn_Unit_0','耗竭魔像仍可承担另一段攻击')
        rejects+=1;step('Inspector/Btn_AssignUnit','已取消第一段不得预留承伤',{'rejections':rejects,'units.plan':''})
        step('Inspector/Btn_AttackSlot','选未取消的冰攻击',{'attackSlot':1})
        step('Inspector/Btn_AssignUnit','真实承伤预案',{'units.plan':'0:1:army_2_001','units.wounds':0})
    advance('Melee',{'wounds':0,'realWounds':0,'units.wounds':0 if blockfirst else 1},True)
    play(21,'strong',{'melee':4,'red':0});target()
    step('Inspector/Btn_Resolve','真实击败女巫',{'kills':1,'fame':4,'ui.Feedback':'攻击结算：有效 4 / 护甲 4\n击败 1，名望 +3。'})
    advance('End',{'won':'True','attacks.cancelled':'','units.banners':'items_001','units.ready':'False','units.wounds':0 if blockfirst else 1,'fame':4},True)

start(9);card('items_001');step('Inspector/Btn_ArtifactBurn','选择原焚毁效果')
for i in range(3):target(i)
step('Inspector/Btn_ArtifactPlay','三名敌人全部攻击被取消',{'removed.ids':'items_001','hand':4,'enemies.skipped':'0,1,2','units.ready':'True','fame':0},True)
advance('Block');advance('AssignDamage');advance('Melee',{'wounds':0,'units.wounds':0},True)
play(21,'strong',{'melee':4,'red':0});play(16,'attack',{'melee':6});play(11,'strong',{'ranged':3,'blue':0});sideways({'melee':7})
for i in range(3):target(i)
step('Inspector/Btn_Resolve','攻击合计10击败三名原敌人',{'kills':3,'fame':8,'ui.Feedback':'攻击结算：有效 10 / 护甲 10\n击败 3，名望 +8。'})
advance('End',{'won':'True','enemies.skipped':'','fame':8,'removed.ids':'items_001','units.ready':'True','wounds':0},True)

request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=240,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
