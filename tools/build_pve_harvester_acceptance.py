"""Original Soul Harvester: per-enemy crystal choices and real kill timing."""
import json
from pathlib import Path
steps=[]
loadout=encounter=0

def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原灵魂收割者：真实击杀、逐敌选晶与攻击/阶段期限') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def start(e):
    global loadout,encounter
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','选择灵魂收割者'))
    while loadout!=20:
        loadout=(loadout+1)%21;step('CampShade/Camp/Btn_Loadout','选择原卡出战',{'draftLoadout':loadout})
    while encounter!=e:
        encounter=(encounter+1)%13;step('CampShade/Camp/Btn_Encounter','切换原遭遇',{'draftEncounter':encounter})
    step('CampShade/Camp/Btn_Start','开始原卡与原敌人',{'phase':'Ranged','source.artifacts':'items_019','hand':7,'fame':0,
        'red':2,'blue':2,'art.cards':'True','art.enemies':'True'})
def advance(p,expected=None,capture=False):step('Inspector/Btn_Next','进入_'+p,{'phase':p,**(expected or {})},capture)
def target(i):step(f'Enemy_{i}/Btn_Target','选择敌人_'+str(i))
def play(n,choice,expected):
    step(f'Hand/Btn_Card_basic_card_{n:03}_0','选择原行动卡');step('Inspector/Btn_Choice_'+choice,'选择原效果');step('Inspector/Btn_Play','正式出牌',expected)
def defend(enemies):
    advance('Block')
    for i,enemy in enumerate(enemies):
        play(16,'strong',{'block':5,'blue':1-i})
        if enemy==29:
            step('Hand/Btn_Card_basic_card_000_0','选择原行进');step('Inspector/Btn_Sideways','横置补足火焰格挡',{'block':6})
        target(i)
        effective,need=(3,3) if enemy==29 else (2,2) if enemy==4 else (5,4)
        step('Inspector/Btn_Resolve','完整格挡原攻击',{'block':0,'ui.Feedback':f'格挡成立：有效 {effective} ≥ {need}。效果随承伤阶段结算。'})
    advance('AssignDamage');advance('Melee',{'wounds':0,'realWounds':0})
def artifact(burn,capture=False):
    step('Hand/Btn_Card_items_019_0','查看原灵魂收割者',capture=capture)
    if burn:step('Inspector/Btn_ArtifactBurn','选择焚毁')
    step('Inspector/Btn_ArtifactPlay','打出原神器',{'melee':8 if burn else 3,'removed.ids':'items_019' if burn else ''})
def choose(colors,expected,capture=False):
    for i,color in enumerate(colors):
        step('Inspector/Btn_KillCrystal','击杀奖励选择_'+color,{'killCrystal.choices':expected[i]},capture and i==len(colors)-1)
def resolve(effective,armor,kills,fame,reward,extra=None,capture=False):
    step('Inspector/Btn_Resolve','实际击杀与奖励',{'kills':kills,'fame':fame,
        'ui.Feedback':f'攻击结算：有效 {effective} / 护甲 {armor}\n击败 {reward[0]}，名望 +{reward[1]}。',**(extra or {})},capture)

for enemy,color in [(29,'Green'),(4,'Blue'),(29,'White')]:
    start(6 if enemy==29 else 7);defend([enemy]);artifact(False,color=='Green')
    play(21,'strong',{'melee':7,'red':1});play(11,'base',{'ranged':1});target(0)
    colors=['Green'] if color=='Green' else ['Green','White'] if color=='White' else ['Green','White','Blue']
    choose(colors,['0:'+x for x in colors],True)
    resources={'red':1,'blue':2 if color=='Blue' else 1,'green':1 if color=='Green' else 0,'white':1 if color=='White' else 0}
    resolve(4,4,1,5,(1,5),resources)
    advance('End',{'won':'True','fame':5,'wounds':0,'realWounds':0,'killCrystal.choices':'',**resources},True)

for burn in [False,True]:
    start(2);defend([32,32]);artifact(burn);target(0);choose(['White'],['0:White'])
    resolve(8 if burn else 3,3,1,2,(1,2),{'white':1},True)
    play(21,'strong',{'melee':4,'red':1});target(1)
    if burn:choose(['White'],['0:White,1:White'])
    resolve(4,3,2,4,(1,2),{'white':2 if burn else 1})
    advance('End',{'won':'True','fame':4,'wounds':0,'realWounds':0,'red':1,'blue':0,'white':2 if burn else 1,
        'killCrystal.choices':'','removed.ids':'items_019' if burn else ''},True)

start(12);defend([29,4]);artifact(True);play(21,'strong',{'melee':12,'red':1});play(21,'strong',{'melee':16,'red':0})
target(0);choose(['Green'],['0:Green'],True);target(0);target(1)
choose(['Green','White','Blue'],['0:Green,1:'+x for x in ['Green','White','Blue']],True)
target(0);resolve(8,8,2,10,(2,10),{'red':0,'blue':1,'green':1,'white':0},True)
advance('End',{'won':'True','fame':10,'wounds':0,'realWounds':0,'red':0,'blue':1,'green':1,'white':0,
    'removed.ids':'items_019','killCrystal.choices':''},True)

request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=360,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
