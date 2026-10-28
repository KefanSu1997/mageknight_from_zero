"""Original hero healing skills: actual battle wounds, unit targets and round markers."""
import json, sys
from pathlib import Path
steps=[]
loadout=rejects=0

def step(path,label,expected=None,capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/'+path,label=label,waitAfterSeconds=.3,skipScreenshot=not capture,
        after=[dict(key=k,expected=str(v),rule='原英雄技能时机、次数、治疗点、真实伤牌、魔晶与战果对应') for k,v in {'ui.truncated':'',**(expected or {})}.items()]))

def advance(phase,expected=None):step('Inspector/Btn_Next','进入_'+phase,{'phase':phase,**(expected or {})})

for kind in range(3):
    for unit in (False,True):
        if '--breeze-visual-only' in sys.argv and (kind != 1 or unit): continue
        desired=46+kind;source=f'hero_skill_pool_00{5 if kind==2 else 4}:{1 if kind==2 else kind:02}'
        skillname=('提神沐浴','提神微风','药剂制作')[kind]
        hero_wounds=(0 if kind==2 else 1) if unit else 2
        (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp','选择战后治疗技能'))
        while loadout!=desired:
            loadout=(loadout+1)%49;step('CampShade/Camp/Btn_Loadout','切换原技能快照',{'draftLoadout':loadout})
        step('CampShade/Camp/Btn_Start','开始实际战斗',{'phase':'Ranged','hero.id':'Goldyx' if kind==2 else 'Wolfhawk','skills.ids':source,
            'skills.used':'False','fame':3,'wounds':0,'healing':0,'hand':4,'blue':1,'white':0,'units.wounds':'0'})
        advance('Block');advance('AssignDamage')
        if unit:
            step('Enemy_0/Btn_Target','选择实际敌人攻击')
            step('Btn_Roster','打开原部队');step('Units/Btn_Unit_0','选择承伤部队',{'ui.unitArtCount':1})
            step('Inspector/Btn_AssignUnit','加入实际承伤顺序')
        advance('Melee',{'wounds':hero_wounds,'realWounds':hero_wounds,'units.wounds':'1' if unit else '0'})
        if unit:step('Btn_Roster','返回原手牌')
        step('Hand/Btn_Card_basic_card_021_0','选择原暴怒');step('Inspector/Btn_Choice_strong','强化攻击')
        step('Inspector/Btn_Play','支付红晶攻击',{'melee':4,'red':0})
        step('Enemy_0/Btn_Target','选择原哨卫');step('Inspector/Btn_Resolve','实际击杀',{'kills':1,'fame':5,
            'ui.Feedback':'攻击结算：有效 4 / 护甲 3\n击败 1，名望 +2。'})
        advance('End',{'won':'True','wounds':hero_wounds,'realWounds':hero_wounds,'fame':5})
        step('Btn_Skills','打开英雄原技能');step('Skills/Btn_Skill_0','核对原技能牌面',{'ui.skillArtCount':1,'ui.Heading':skillname},not unit)
        step('Inspector/Btn_SkillChoice_0','选择原印刷效果')
        step('Inspector/Btn_UseSkill','获得治疗点而非自动治疗',{'healing':2 if kind==2 else 1,'healing.grail':0,
            'skills.used':'True','wounds':hero_wounds,'units.wounds':'1' if unit else '0','blue':2 if kind==0 else 1,'white':1 if kind==1 else 0},not unit)
        rejects+=1;step('Inspector/Btn_UseSkill','本轮再次使用必须拒绝',{'rejections':rejects,'ui.Feedback':'该技能本轮已经翻面使用','healing':2 if kind==2 else 1})
        step('Btn_Recovery','按需打开战后治疗',{'ui.Heading':'战后治疗','support.target':-1})
        if unit:step('Inspector/Btn_UnitSupportTarget','选择真实部队伤牌',{'support.target':0})
        for i in range(1 if unit or kind!=2 else 2):
            step('Inspector/Btn_HealWound','消费原技能治疗点',{'healing':0 if unit else (2 if kind==2 else 1)-i-1,
                'wounds':hero_wounds if unit else 1-i,'realWounds':hero_wounds if unit else 1-i,'units.wounds':'0','fame':5},unit or i==(1 if kind==2 else 0))
        step('Skills/Btn_Skill_0','从治疗面板返回原技能',{'ui.Heading':skillname,'skills.used':'True','healing':0})

request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=300,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
