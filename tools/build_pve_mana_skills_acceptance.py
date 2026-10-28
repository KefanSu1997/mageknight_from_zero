"""Original Dark Fire / Leaves in Wind / Treetop: actual token/crystal spending."""
import json
from pathlib import Path
steps=[]
loadout=encounter=rejects=0

def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原技能两类资源、实际支付、每轮标记和最终战斗') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def advance(phase,expected=None):step('Inspector/Btn_Next','进入_'+phase,{'phase':phase,**(expected or {})})
def play(n,expected):
    step(f'Hand/Btn_Card_basic_card_{n:03}_0','选择真实原行动卡')
    step('Inspector/Btn_Choice_strong','选择印刷强化');step('Inspector/Btn_Play','实际支付原卡费用',expected,True)
def attack(i,power,kills,fame,reward=2):
    step(f'Enemy_{i}/Btn_Target','选择实际敌人')
    step('Inspector/Btn_Resolve','资源消费并实际击杀',{'kills':kills,'fame':fame,'ui.Feedback':f'攻击结算：有效 {power} / 护甲 {4 if reward==3 else 3}\n击败 1，名望 +{reward}。'})
def reject():
    global rejects
    rejects+=1
    step('Inspector/Btn_UseSkill','每轮翻面禁止重复使用',{'rejections':rejects,'skills.used':'True','ui.Feedback':'该技能本轮已经翻面使用'})

for kind,l,e in [(0,54,2),(1,55,10),(2,56,2),(3,57,2)]:
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','选择原技能快照'))
    while loadout!=l:
        loadout=(loadout+1)%58;step('CampShade/Camp/Btn_Loadout','切换原出战',{'draftLoadout':loadout})
    while encounter!=e:
        encounter=(encounter+1)%13;step('CampShade/Camp/Btn_Encounter','切换原遭遇',{'draftEncounter':encounter})
    step('CampShade/Camp/Btn_Start','开始原魔力技能战斗',{'phase':'Ranged','hero.id':'Arythea' if kind<2 else 'Norowas',
        'skills.ids':'hero_skill_pool_000:04' if kind<2 else 'hero_skill_pool_002:0'+str(5 if kind==3 else 4),
        'skills.used':'False','red':0,'green':0,'white':0,'blue':1 if kind==0 else 0,'hand':4,'fame':3,'units.ready':'True','art.cards':'True','art.enemies':'True'})
    step('Btn_Skills','显示原技能');step('Skills/Btn_Skill_0','核对原技能牌面',{'ui.skillArtCount':1})
    step('Inspector/Btn_SkillChoice_'+str(1 if kind==1 else 0),'选择原印刷魔力颜色')
    resources={'red':1,'tokens.red':1,'tokens.black':0} if kind==0 else {'red':1,'tokens.black':1,'tokens.red':0} if kind==1 else {'green':1,'tokens.white':1,'white':0} if kind==2 else {'white':1,'tokens.green':1,'green':0}
    step('Inspector/Btn_UseSkill','原技能取得晶体与临时魔力',{'skills.used':'True',**resources},True)
    reject();step('Btn_Skills','返回实际卡牌')
    if kind<2:
        step('Btn_Roster','显示真实部队');step('Units/Btn_Unit_0','选择原部队',{'ui.unitArtCount':1})
        if kind==1:
            step('Inspector/Btn_UnitMode_2','选择黑费战斗转换');step('Inspector/Btn_UnitConversion','选择转攻城')
        step('Inspector/Btn_UnitAbility_'+str(1 if kind==0 else 2),'消费技能临时魔力',{'units.ready':'False','red':1,
            **({'ranged':6,'tokens.red':0} if kind==0 else {'tokens.black':0,'attacks.siege':'True'})},True)
        step('Btn_Roster','返回原手牌')
        if kind==0:
            attack(0,6,1,5);advance('Block');play(16,{'block':5,'blue':0,'red':1})
            step('Enemy_1/Btn_Target','选择剩余敌人攻击');step('Inspector/Btn_Resolve','强化格挡实际保护英雄',{'block':0,'ui.Feedback':'格挡成立：有效 5 ≥ 4。效果随承伤阶段结算。'})
            advance('AssignDamage');advance('Melee',{'wounds':0,'realWounds':0})
        play(21,{'red':0,'tokens.red':0,'melee' if kind==0 else 'siege':4})
        attack(1 if kind==0 else 0,4,2 if kind==0 else 1,7 if kind==0 else 6,2 if kind==0 else 3)
    else:
        play(7,{'ranged':3,'tokens.white':0,'white':0,**({'green':1} if kind==2 else {'tokens.green':1})})
        attack(0,3,1,5)
        step('Btn_Roster','显示原凶蛮僧侣');step('Units/Btn_Unit_0','选择真实部队',{'ui.unitArtCount':1})
        step('Inspector/Btn_UnitAbility_1','消费技能绿色资源发动攻城',{'siege':4,'green':0,'tokens.green':0,'units.ready':'False'},True)
        attack(1,4,2,7)
    if kind!=0:advance('Block');advance('AssignDamage');advance('Melee')
    step('Inspector/Btn_Next','实际战斗最终结果',{'phase':'End','won':'True','wounds':0,'realWounds':0,
        'fame':6 if kind==1 else 7,'skills.used':'True','units.wounds':'0','red':0,'green':0,'white':0,
        'tokens.red':0,'tokens.green':0,'tokens.white':0,'tokens.black':0},True)
    step('Btn_Skills','战后检查每轮技能');step('Skills/Btn_Skill_0','仍是原技能且已翻面');reject()

request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
