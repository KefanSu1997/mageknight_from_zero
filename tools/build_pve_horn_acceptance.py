"""Original Wrath Horn: real seeded dice, bonus choice, wounds and fortified kills."""
import json
from pathlib import Path
steps=[];loadout=encounter=0

def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原愤怒号角：攻城数值、逐点投骰、红黑真实创伤、原卡牌区与实际战斗') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def advance(p,expected=None,capture=False):step('Inspector/Btn_Next','进入_'+p,{'phase':p,**(expected or {})},capture)
def start(l,e,seed):
    global loadout,encounter
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','选择原号角出战'))
    while loadout!=l:
        loadout=(loadout+1)%42;step('CampShade/Camp/Btn_Loadout','切换原神器快照',{'draftLoadout':loadout})
    while encounter!=e:
        encounter=(encounter+1)%13;step('CampShade/Camp/Btn_Encounter','切换真实敌人',{'draftEncounter':encounter})
    step('CampShade/Camp/Btn_Start','开始号角战斗',{'phase':'Ranged','hand':5,'source.artifacts':'items_009','replay.seed':seed,'dice.count':0,
        'red':1,'blue':1,'wounds':0,'realWounds':0,'art.cards':'True','art.enemies':'True'})

for l,seed,burn,bonus,faces,wounds,melee in [(39,0,False,0,'Gold',0,True),(39,0,False,0,'Gold',0,False),
        (39,0,True,0,'',0,False),(39,0,True,2,'Gold,Gold',0,False),(40,6,False,0,'Black',1,False),(41,2,True,5,'Gold,Green,Red,Black,Red',3,False)]:
    start(l,0 if melee else 10,seed)
    if melee:
        advance('Block');step('Hand/Btn_Card_basic_card_016_0','选择原决心');step('Inspector/Btn_Choice_strong','强化格挡')
        step('Inspector/Btn_Play','实际支付蓝晶格挡',{'blue':0,'block':5})
        step('Enemy_0/Btn_Target','选择实际敌人攻击');step('Inspector/Btn_Resolve','真实格挡',{'ui.Feedback':'格挡成立：有效 5 ≥ 4。效果随承伤阶段结算。'})
        advance('AssignDamage');advance('Melee',{'wounds':0})
    step('Hand/Btn_Card_items_009_0','选择原愤怒号角',{'ui.cardArtCount':5 if not melee else 4})
    step('Inspector/Btn_ArtifactBurn' if burn else 'Inspector/Btn_ArtifactNormal','选择原神器模式')
    if burn:
        for n in range(bonus):step('Inspector/Btn_HornBonus','选择额外攻城与骰数',{'horn.bonus':n+1})
    step('Inspector/Btn_ArtifactPlay','正式出牌投骰与创伤',{'siege':5+bonus,'ranged':0,'dice.faces':faces,'dice.count':bonus if burn else 1,
        'wounds':wounds,'realWounds':wounds,'hand':4+wounds-(1 if melee else 0),'removed.ids':'items_009' if burn else '',
        'played':(1 if melee else 0)+(0 if burn else 1),'red':1,'blue':0 if melee else 1},True)
    step('Enemy_0/Btn_Target','选择真实攻击目标');step('Inspector/Btn_Resolve','攻城资源真正击杀',{'kills':1,'fame':2 if melee else 3,
        'ui.Feedback':f'攻击结算：有效 {5+bonus} / 护甲 {3 if melee else 4}\n击败 1，名望 +{2 if melee else 3}。'})
    if not melee:advance('Block');advance('AssignDamage');advance('Melee',{'wounds':wounds,'realWounds':wounds})
    advance('End',{'won':'True','fame':2 if melee else 3,'wounds':wounds,'realWounds':wounds,'removed.ids':'items_009' if burn else ''},wounds>0 or burn and bonus==0)
request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=360,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
