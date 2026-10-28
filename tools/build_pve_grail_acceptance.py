"""Golden Grail: actual combat wounds, source-specific healing Fame and real deck draws."""
import json
from pathlib import Path
steps=[]
loadout=rejects=0
roster=False
def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原黄金圣杯费用、治疗来源、实际伤牌/牌库、名望和战斗对应') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def show(want):
    global roster
    if roster!=want:step('Btn_Roster','查看原部队' if want else '返回原手牌');roster=want
def unit(i):show(True);step(f'Units/Btn_Unit_{i}','选择原部队')
def card(n):show(False);step(f'Hand/Btn_Card_basic_card_{n:03}_0','选择原行动卡')
def play(n,expected):
    card(n);step('Inspector/Btn_Choice_strong','选择原强化');step('Inspector/Btn_Play','实际付费出牌',expected)
def advance(phase,extra=None):step('Inspector/Btn_Next','进入_'+phase,{'phase':phase,**(extra or {})})
def resolve(power,block=False,fame=2):
    step('Enemy_0/Btn_Target','选择原哨卫')
    msg=f'格挡成立：有效 {power} ≥ 4。效果随承伤阶段结算。' if block else f'攻击结算：有效 {power} / 护甲 3\n击败 1，名望 +2。'
    step('Inspector/Btn_Resolve','实际格挡' if block else '实际击杀',{'ui.Feedback':msg,**({'block':0} if block else {'kills':1,'fame':fame})})
for mode in ('base_hero','base_unit','burn_hero','burn_unit','mixed'):
    mixed=mode=='mixed';burn=mode.startswith('burn');unitw=mode.endswith('unit') or mixed;herow=not mode.endswith('unit')
    desired=45 if mixed else 44;initial=8 if mixed else 0;fame=initial+2
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','选择黄金圣杯出战'))
    while loadout!=desired:
        loadout=(loadout+1)%46;step('CampShade/Camp/Btn_Loadout','切换出战',{'draftLoadout':loadout})
    step('CampShade/Camp/Btn_Start','开始原圣杯快照',{'phase':'Ranged','source.artifacts':'items_012','deck.count':2,'hand':5,'fame':initial,'healing':0,'wounds':0})
    roster=False
    advance('Block')
    if not herow:play(16,{'block':5,'blue':0});resolve(5,True)
    advance('AssignDamage');advance('Melee',{'wounds':2 if herow else 0,'realWounds':2 if herow else 0})
    if unitw:
        unit(1 if mixed else 0);step('Inspector/Btn_UnitAbility_1','剑士实际自伤攻击',{'melee':6,'units.wounds':'0,1' if mixed else '1'});resolve(6,fame=fame)
    else:play(21,{'melee':4,'red':0});resolve(4,fame=fame)
    advance('End',{'won':'True','fame':fame,'deck.count':2})
    show(False);step('Hand/Btn_Card_items_012_0','选择原黄金圣杯',{'ui.Heading':'黄金圣杯'},mode=='base_hero')
    step('Inspector/Btn_ArtifactBurn' if burn else 'Inspector/Btn_ArtifactNormal','选择焚毁' if burn else '选择普通效果')
    step('Inspector/Btn_ArtifactPlay','打出实际原圣杯',{'healing':6 if burn else 2,'healing.grail':0 if burn else 2,'fame':fame,
        'drawPerHeal':1 if burn else 0,'deck.count':2,'removed.ids':'items_012' if burn else ''},True)
    if mixed:
        unit(0);step('Inspector/Btn_UnitAbility_0','药草师增加普通治疗点',{'healing':4,'healing.grail':2,'green':0,'units.ready':'False,False','fame':10})
        show(False)
    step('Btn_Recovery','选择真实治疗目标',{'ui.Heading':'战后治疗','support.target':-1})
    if mixed:step('Inspector/Btn_HealingOrder','先消费普通治疗点',{'healing.grailFirst':'False'})
    if herow:
        for i in range(2):
            step('Inspector/Btn_HealWound','治疗真实手牌创伤',{'wounds':1-i,'realWounds':1-i,'healing':(6 if burn else 4 if mixed else 2)-i-1,
                'healing.grail':2 if mixed else 0 if burn else 1-i,'fame':fame if burn or mixed else fame+i+1,
                'deck.count':1-i if burn else 2},i==1)
        if burn:step('Btn_Recovery','查看真实抽出的原牌',{'hand':5,'ui.cardArtCount':5,
            'hand.ids':'basic_card_016,basic_card_000,basic_card_011,basic_card_000,basic_card_011'},True)
    if unitw:
        step('Inspector/Btn_UnitSupportTarget','选择治疗部队',{'support.target':0})
        if mixed:step('Inspector/Btn_UnitSupportTarget','选择受伤II级剑士',{'support.target':1})
        step('Inspector/Btn_HealWound','按II级支付两点治疗',{'healing':4 if burn else 0,'healing.grail':0,'units.wounds':'0,0' if mixed else '0',
            'units.ready':'False,False' if mixed else 'False','fame':fame if burn else fame+2,'deck.count':2,'hand':4 if mixed else 3},True)
    if not burn:
        rejects+=1;step('Inspector/Btn_HealWound','无伤牌或无治疗点不能重复获益',{'rejections':rejects,'healing':0,'fame':fame+2})
request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
