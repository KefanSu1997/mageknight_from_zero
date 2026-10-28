"""Four original Necro Shield choices; actual distinct attack slots and card zones."""
import json
from pathlib import Path
steps=[]
loadout=encounter=rejects=0

def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原亡君之盾、按攻击槽逐次消费、真实牌区与战斗结果') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def start(burn,split):
    global loadout,encounter
    e=(11 if split else 6) if burn else (2 if split else 0)
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','选择原亡君之盾'))
    while loadout!=19:
        loadout=(loadout+1)%20;step('CampShade/Camp/Btn_Loadout','切换盾牌配置',{'draftLoadout':loadout})
    while encounter!=e:
        encounter=(encounter+1)%12;step('CampShade/Camp/Btn_Encounter','切换原遭遇',{'draftEncounter':encounter})
    step('CampShade/Camp/Btn_Start','开始原盾与原敌人',{'phase':'Ranged','source.artifacts':'items_020','hand':5,'fame':0,
        'red':1,'blue':1,'art.cards':'True','art.enemies':'True'})
def advance(phase,expected=None,capture=False):step('Inspector/Btn_Next','进入_'+phase,{'phase':phase,**(expected or {})},capture)
def target(t):step(f'Enemy_{t}/Btn_Target','选择敌人_'+str(t))
def play(n,choice,expected):
    step(f'Hand/Btn_Card_basic_card_{n:03}_0','选择原行动卡');step('Inspector/Btn_Choice_'+choice,'选择原效果');step('Inspector/Btn_Play','正式出牌',expected)
def sideways(expected):
    step('Hand/Btn_Card_basic_card_000_0','选择原行进');step('Inspector/Btn_Sideways','原行进横置',expected)

for burn,split in [(False,False),(False,True),(True,False),(True,True)]:
    start(burn,split);advance('Block')
    step('Hand/Btn_Card_items_020_0','选择原神器与牌面')
    if burn:step('Inspector/Btn_ArtifactBurn','选择原焚毁效果')
    if split:step('Inspector/Btn_ShieldMode','选择分次格挡')
    pool=(12 if burn else 8) if split else (8 if burn else 6)
    step('Inspector/Btn_ArtifactPlay',('焚毁' if burn else '普通')+('分次' if split else '单次'),
        {'block':pool,'hand':4,'removed.ids':'items_020' if burn else '', 'played.ids':'' if burn else 'items_020'},True)
    attacks=[(0,0,3),(0,1,3),(1,0,4)] if burn and split else [(0,0,4),(1,0,4)] if split else [(0,0,3 if burn else 4)]
    effective=4 if split else 8 if burn else 6
    for t,a,need in attacks:
        target(t)
        if a:step('Inspector/Btn_AttackSlot','选择同敌第二次攻击',{'attackSlot':a})
        pool-=effective
        step('Inspector/Btn_Resolve',f'实际格挡_{t}_{a}',{'block':pool,
            'ui.Feedback':f'格挡成立：有效 {effective} ≥ {need}。效果随承伤阶段结算。'},burn and split)
        if t==0 and a==0:
            target(t);rejects+=1
            step('Inspector/Btn_Resolve','重复格挡同一次攻击被拒绝',{'block':pool,'rejections':rejects,'ui.Feedback':'该次攻击已经格挡'})
    advance('AssignDamage');advance('Melee',{'wounds':0,'realWounds':0,'block':0})
    play(21,'strong',{'melee':4,'red':0})
    if burn and not split:
        play(11,'strong',{'ranged':3,'blue':0});sideways({'melee':5})
    fame=6 if burn and not split else 3 if burn else 2
    armor=4 if burn else 3
    target(0);step('Inspector/Btn_Resolve','原卡反击第一敌人',{'kills':1,'fame':fame,
        'ui.Feedback':f'攻击结算：有效 4 / 护甲 {armor}\n击败 1，名望 +{fame}。'})
    if split:
        play(16,'attack',{'melee':2});sideways({'melee':3});target(1)
        step('Inspector/Btn_Resolve','击败第二名原敌人',{'kills':2,'fame':fame+2,
            'ui.Feedback':'攻击结算：有效 3 / 护甲 3\n击败 1，名望 +2。'});fame+=2
    advance('End',{'won':'True','fame':fame,'wounds':0,'realWounds':0,'block':0,'red':0,
        'blue':0 if burn and not split else 1,'removed.ids':'items_020' if burn else ''},True)

request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
