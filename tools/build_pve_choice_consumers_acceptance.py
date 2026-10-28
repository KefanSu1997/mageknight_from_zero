"""Two legal original builds exercise inputs, actual consumers and persistent recovery."""
import json,sys
from pathlib import Path
steps=[]
def click(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,after=[dict(key=k,expected=str(v),rule='原材料按实体弃置不算治疗；自然之力实际部队物抗及战末清理；魔晶力量按支付后套数独立选奖励；爆裂箭矢实际击杀再选幸存目标减甲') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))
def setup(units):
    (click('Btn_Menu', '打开战斗菜单'), click('Btn_Camp','原卡输入与后续消费'));click('CampShade/Camp/Btn_Loadout','已有合法资源构筑');click('CampShade/Picker/Btn_Select_1','红1蓝2白1绿0')
    if units:
        click('CampShade/Camp/Btn_ChooseUnits','选择一支原部队');click('CampShade/UnitPicker/Btn_Unit_army_2_006','真实一级农民');click('CampShade/UnitPicker/Btn_Back','保留实际部队')
    p='CampShade/DeedPicker/';click('CampShade/Camp/Btn_ChooseCards','七原牌');click(p+'Btn_ClearDeeds','清空待选')
    for id in ['basic_card_006','basic_card_008']:click(p+'Btn_ChooseDeed_'+id,'原牌 '+id)
    click(p+'Btn_DeedNext','基础第二页');click(p+'Btn_ChooseDeed_basic_card_021','原狂怒');click(p+'Btn_DeedKind_1','高级行动')
    for i in range(3):click(p+'Btn_DeedNext','高级下一页')
    for id in ['advanced_card_037','advanced_card_038','advanced_card_040','advanced_card_041']:click(p+'Btn_ChooseDeed_'+id,'原牌 '+id)
    click(p+'Btn_DoneDeeds','确认原牌构筑');click('CampShade/Camp/Btn_Journey','建立连续玩家');click('Tactic_day_1/Btn_Tactic_day_1','原战术');click('Btn_BeginTurn','正式第一回合');click('Btn_JourneyHand','实际手牌')
def outside(index,choice):
    click('JourneyHand/Btn_Hand_'+str(index),'选择手牌 '+str(index));click('JourneyHand/Btn_OutsideChoice_'+choice,'原效果 '+choice);click('JourneyHand/Btn_PlayOutside','确认原效果入口')
def card(id,choice):
    click('Hand/Btn_Card_'+id+'_0','原牌 '+id);click('Inspector/Btn_Choice_'+choice,'原选择 '+choice);click('Inspector/Btn_Play','原费用与正式出牌')
def confirm(label,expected=None,capture=False):click('DeedInputs/Btn_ConfirmInputs',label,expected,capture)
def attack(i,label,expected=None,capture=False):
    click('Enemy_'+str(i)+'/Btn_Target','当前实际敌人 '+str(i));click('Inspector/Btn_Resolve',label,expected,capture)
def move():
    click('JourneyHand/Btn_CloseHand','返回实际地图');click('Btn_JourneyMap','地图');click('JourneyMap/Btn_Hex_plains','实际平原村庄');click('JourneyMap/Btn_Move','支付2移动',{'journey.position':'0,1','movement':0});click('JourneyMap/Btn_CloseMap','返回旅程')

setup(True)
outside(0,'green_crystal');outside(2,'base_move');confirm('不屈不挠不弃牌，原移动2',{'movement':2},True);move()
click('Btn_JourneyEncounter_2','两名原兽人骷髅战士');card('advanced_card_041','strong_red');attack(0,'爆裂箭矢红费远程3击杀第一敌人',{'explosive.pending':1,'red':0,'kills':1},True)
click('Enemy_1/Btn_Target','原索引1的实际幸存敌人');click('Inspector/Btn_ExplosiveArmor','实际目标护甲3降至2',{'explosive.pending':0,'enemies.armor':'3,2'},True)
card('advanced_card_038','base');click('DeedInputs/Btn_InputUnit_0','选择实际自有农民原图',capture=True);confirm('本场物理抗性，不消耗部队',{'units.physicalResist':'True','units.ready':'True'})
click('Inspector/Btn_Next','格挡阶段');click('Inspector/Btn_Next','分配真实未格挡伤害');click('Btn_Roster','查看真实农民');click('Units/Btn_Unit_0','同一实际农民');click('Enemy_1/Btn_Target','存活敌人的物理攻击4');click('Inspector/Btn_AssignUnit','实际部队承伤计划');click('Inspector/Btn_Next','抗性吸收后农民一伤，英雄无伤',{'units.wounds':'1','units.ready':'True','wounds':0},True)
click('Btn_Roster','回原手牌');card('basic_card_021','attack');attack(1,'原攻击2实际消费减甲',{'kills':2,'fame':4});click('Inspector/Btn_Next','战斗结束清物抗，真实伤保留',{'units.physicalResist':'False','units.wounds':'1'})
card('advanced_card_040','strong_heal_Blue');confirm('红晶已用于箭矢，付款后零套，治疗2',{'blue':1,'healing':2});click('Btn_Recovery','战后实际治疗');click('Inspector/Btn_UnitSupportTarget','选择同一受伤农民');click('Inspector/Btn_HealWound','按原等级1治疗，恢复发动资格',{'units.wounds':'0','units.canActivate':'True','healing':1},True);click('Inspector/Btn_EndTurn','同一玩家正式结束回合',{'journey.completedTurns':1,'hand.ids':'basic_card_008'})
click('Btn_BeginTurn','保留同一部队进入第二回合');click('Btn_JourneyEncounter_0','另一真实原敌人');click('Inspector/Btn_Next','格挡阶段');click('Inspector/Btn_Next','未格挡');click('Inspector/Btn_Next','真实承伤',{'wounds':2});click('Btn_Roster','查看已治疗同一部队');click('Units/Btn_Unit_0','原农民');click('Inspector/Btn_UnitAbility_0','治疗后实际攻击2并耗竭',{'melee':2,'units.ready':'False'},True);click('Btn_Roster','原承诺横置');click('Hand/Btn_Card_basic_card_008_0','真实剩余原牌');click('Inspector/Btn_Sideways','横置普通攻击1');attack(0,'农民与原牌合计3真实击杀',{'kills':1,'fame':6});click('Inspector/Btn_Next','完成第二战');click('Inspector/Btn_EndTurn','同玩家两战两回合保留伤牌和耗竭',{'journey.completedTurns':2,'journey.completedBattles':2,'wounds':2,'units.ready':'False'},True)

second_route_start=len(steps)
setup(False)
outside(0,'green_crystal');outside(5,'base');outside(4,'strong_heal_Blue');click('DeedInputs/Btn_InputBonus_0_0','治疗主效果独立选择套色移动奖励',capture=True);confirm('真实蓝晶2变1仍保留一套四色：治疗2加移动2',{'blue':1,'green':1,'red':2,'white':2,'healing':2,'movement':2},True);click('JourneyHand/Btn_HealHero','消费主治疗点处理原箭矢自伤',{'wounds':0,'healing':1});move()
click('Btn_JourneyEncounter_2','新合法构筑双敌');click('Inspector/Btn_Next','格挡阶段');card('advanced_card_038','strong_block');confirm('自然之力绿费格挡6',{'green':0,'block':6});attack(0,'原格挡6防御第一攻击');click('Inspector/Btn_Next','另一真实攻击未格挡');click('Inspector/Btn_Next','真实两伤且战前余治疗不能留入战斗',{'wounds':2,'healing':0})
card('advanced_card_037','base_attack');click('DeedInputs/Btn_InputMaterial_0','选择一张实际伤牌作为可选材料',capture=True);confirm('基础2加弃伤1，实际伤进弃牌不算治疗',{'melee':3,'wounds':1,'discardWounds':1},True);attack(0,'原攻击3击杀护甲3敌人');card('basic_card_021','strong');attack(1,'使用原箭矢得到的红晶强化狂怒，击杀第二敌人',{'red':1,'kills':2,'fame':4});click('Inspector/Btn_Next','结束实际战斗');click('Inspector/Btn_EndTurn','伤牌弃置与手牌创伤分开保留',{'hand':2,'wounds':1,'journey.discardCount':7,'journey.completedTurns':1},True)
if '--second' in sys.argv:
    steps=steps[second_route_start:]
else:
    steps=steps[:second_route_start]
if '--material-display' in sys.argv:
    steps=steps[:53]
    for s in steps:s['after']=[];s['skipScreenshot']=True
    click('DeedInputs/Btn_InputMaterial_1','同名第二张伤牌独立选中，第一张取消',capture=True)
    click('DeedInputs/Btn_InputMaterial_0','改选同名第一张伤牌，第二张取消',capture=True)
if '--from' in sys.argv:
    for s in steps[:int(sys.argv[sys.argv.index('--from')+1])]:s['after']=[];s['skipScreenshot']=True
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,defaultWaitAfterSeconds=.3,maxRunSeconds=360,useRunSubfolder=False,steps=steps),ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps',sum(len(s['after']) for s in steps),'checks')
