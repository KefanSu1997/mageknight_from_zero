"""Original Peasants/Thugs: combat contributions, reputation and real influence payment."""
import json,sys
from pathlib import Path
steps=[]
loadout=rejects=0
def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原部队实际能力、声誉、承伤费用与最终结算') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def advance(phase,expected=None):step('Inspector/Btn_Next','进入_'+phase,{'phase':phase,**(expected or {})})
def play(n,mode='strong',expected=None):
    step(f'Hand/Btn_Card_basic_card_{n:03}_0','选择原行动卡')
    step('Inspector/Btn_Choice_'+mode,'选择原印刷效果');step('Inspector/Btn_Play','实际原卡出牌',expected)
def select_unit(i=0):step('Units/Btn_Unit_'+str(i),'选择原部队',{'ui.unitArtCount':2 if kind==4 else 1})
def reject(path,expected):
    global rejects
    rejects+=1;step(path,'拒绝非法重复或费用不足',{'rejections':rejects,**expected})
def sideways():
    step('Hand/Btn_Card_basic_card_000_0','选择原进军');step('Inspector/Btn_Sideways','横置原牌补充1')
for kind,l in [(0,58),(1,58),(2,59),(3,59),(5,59),(4,60)]:
    if len(sys.argv)>1 and kind!=int(sys.argv[1]):continue
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','配置本条验收'))
    while loadout!=l:
        loadout=(loadout+1)%61;step('CampShade/Camp/Btn_Loadout','切换原出战',{'draftLoadout':loadout})
    step('CampShade/Camp/Btn_Start','开始真实遭遇',{'phase':'Ranged','fame':8 if kind==4 else 3,'influence':0,'reputation':0,
        'wounds':0,'units.ready':'True,True' if kind==4 else 'True','units.wounds':'0,0' if kind==4 else '0',
        'source.units':'army_2_006,army_2_011' if kind==4 else 'army_2_006' if kind<2 else 'army_2_011'})
    advance('Block')
    if kind in (0,2):
        play(16,expected={'block':5,'blue':0});step('Enemy_0/Btn_Target','选择未格挡攻击');step('Inspector/Btn_Resolve','真实格挡',{'block':0})
    if kind in (1,3):
        step('Btn_Roster','打开部队');select_unit()
        step('Inspector/Btn_UnitAbility_0','原部队格挡',{'block':2 if kind==1 else 3,'units.ready':'False','reputation':0},True)
        step('Btn_Roster','返回真实手牌')
        if kind==1:play(16,'block',{'block':4})
        else:sideways()
        step('Enemy_0/Btn_Target','选择实际攻击');step('Inspector/Btn_Resolve','总格挡4实际保护英雄',{'block':0,'ui.Feedback':'格挡成立：有效 4 ≥ 4。效果随承伤阶段结算。'})
    advance('AssignDamage')
    if kind in (4,5):
        step('Btn_Roster','打开承伤部队');select_unit(1 if kind==4 else 0)
        if kind==5:
            step('Inspector/Btn_UnitAbility_2','暴徒原影响力并扣声誉',{'influence':4,'reputation':-1,'units.ready':'False'},True)
            reject('Inspector/Btn_UnitAbility_2',{'influence':4,'reputation':-1})
        step('Enemy_0/Btn_Target','选择真实未格挡攻击');step('Inspector/Btn_AssignUnit','预选暴徒承伤但未扣款',{'influence':0 if kind==4 else 4})
        if kind==4:
            reject('Inspector/Btn_Next',{'phase':'AssignDamage','influence':0,'wounds':0,'units.wounds':'0,0'})
            select_unit(0);step('Inspector/Btn_UnitAbility_1','农民原影响力供实际承伤付款',{'influence':2,'reputation':0,'units.ready':'False,True'},True)
            reject('Inspector/Btn_UnitAbility_1',{'influence':2,'reputation':0})
        advance('Melee',{'influence':0 if kind==4 else 2,'wounds':0,'realWounds':0,'units.wounds':'0,1' if kind==4 else '1'})
        select_unit(1 if kind==4 else 0)
        reject('Inspector/Btn_UnitAbility_1',{'reputation':0 if kind==4 else -1,'influence':0 if kind==4 else 2})
        step('Btn_Roster','返回真实手牌')
    else:advance('Melee',{'wounds':0,'realWounds':0})
    if kind in (0,2):
        step('Btn_Roster','打开实际部队');select_unit()
        ability=0 if kind==0 else 1
        step('Inspector/Btn_UnitAbility_'+str(ability),'原部队攻击',{'melee':2 if kind==0 else 3,'reputation':0 if kind==0 else -1,'units.ready':'False'},True)
        reject('Inspector/Btn_UnitAbility_'+str(ability),{'reputation':0 if kind==0 else -1,'melee':2 if kind==0 else 3})
        step('Btn_Roster','返回真实手牌')
        if kind==0:sideways()
    else:play(21,expected={'melee':4,'red':0})
    step('Enemy_0/Btn_Target','选原敌人');step('Inspector/Btn_Resolve','消费实际攻击击杀',{'kills':1,'fame':10 if kind==4 else 5,'melee':0})
    step('Inspector/Btn_Next','最终结算',{'phase':'End','won':'True','wounds':0,'realWounds':0,'reputation':-1 if kind in (2,5) else 0,
        'influence':2 if kind==5 else 0,'units.wounds':'0,1' if kind==4 else '1' if kind==5 else '0'},True)
request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=360,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
