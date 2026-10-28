"""Original Courage Banner: real combat exhaustion, then targeted/all-unit readiness without healing."""
import json
from pathlib import Path
steps=[]
loadout=rejects=0
roster=False
def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='勇气旌旗原附着/战外重整/每轮一次/伤牌不治疗') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def start():
    global loadout,roster
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','选择勇气旌旗出战'))
    while loadout!=29:
        loadout=(loadout+1)%30;step('CampShade/Camp/Btn_Loadout','选择原卡快照',{'draftLoadout':loadout})
    step('CampShade/Camp/Btn_Start','开始真实战斗',{'phase':'Ranged','hand':5,'fame':8,'units.ready':'True,True','units.wounds':'0,0','units.banners':'none,none','source.artifacts':'items_003'})
    roster=False
def show(want):
    global roster
    if roster!=want:step('Btn_Roster','查看部队' if want else '返回手牌');roster=want
def card(id):show(False);step('Hand/Btn_Card_'+id+'_0','选择原卡_'+id)
def unit(i=0):show(True);step('Units/Btn_Unit_'+str(i),'选择实际部队_'+str(i),{'ui.unitArtCount':2})
def advance(p,expected=None,capture=False):step('Inspector/Btn_Next','进入_'+p,{'phase':p,**(expected or {})},capture)
for burn in (False,True):
    for wound in (False,True):
        start();card('items_003')
        if burn:
            step('Inspector/Btn_ArtifactBurn','选择原焚毁重整');rejects+=1
            step('Inspector/Btn_ArtifactPlay','战中焚毁拒绝',{'rejections':rejects,'hand':5,'removed.ids':'','units.ready':'True,True'},True)
        else:
            step('Inspector/Btn_ArtifactPlay','原勇气旌旗实际附着',{'hand':4,'played':0,'units.banners':'items_003,none'},True)
            unit();rejects+=1
            step('Inspector/Btn_Courage','战中普通重整拒绝',{'rejections':rejects,'units.courageSpent':'False,False','units.ready':'True,True'})
        advance('Block');card('basic_card_016');step('Inspector/Btn_Choice_strong','选择决心强化');step('Inspector/Btn_Play','支付蓝魔力格挡',{'block':5,'blue':1})
        step('Enemy_0/Btn_Target','选择原哨卫');step('Inspector/Btn_Resolve','正式完整格挡',{'block':0})
        advance('AssignDamage');advance('Melee',{'wounds':0,'realWounds':0})
        for i in (0,1):
            unit(i);step('Inspector/Btn_UnitAbility_'+str(1 if wound else 0),'两支部队实际出招耗竭',{'melee':(6 if wound else 3)*(i+1),
                'units.ready':'False,True' if i==0 else 'False,False','units.wounds':('1,0' if i==0 else '1,1') if wound else '0,0'})
        step('Enemy_0/Btn_Target','选择击杀目标');step('Inspector/Btn_Resolve','实际击杀领取名望',{'kills':1,'fame':10})
        wounds='1,1' if wound else '0,0'
        advance('End',{'won':'True','fame':10,'units.ready':'False,False','units.wounds':wounds},True)
        if burn:
            card('items_003');step('Inspector/Btn_ArtifactBurn','战后选择原焚毁')
            step('Inspector/Btn_ArtifactPlay','焚毁真实神器重整全部部队',{'removed.ids':'items_003','units.ready':'True,True','units.wounds':wounds,
                'units.canActivate':'False,False' if wound else 'True,True'},True)
            unit();step('Units/Btn_Unit_1','核对两支部队就绪与伤牌',{'units.ready':'True,True','units.wounds':wounds},True)
        else:
            unit();step('Inspector/Btn_Courage','战后重整附旗部队',{'units.ready':'True,False','units.wounds':wounds,'units.courageSpent':'True,False',
                'units.canActivate':'False,False' if wound else 'True,False'},True)
            rejects+=1;step('Inspector/Btn_Courage','本轮已用拒绝重复重整',{'rejections':rejects,'units.ready':'True,False','units.wounds':wounds,'units.courageSpent':'True,False'})
request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
