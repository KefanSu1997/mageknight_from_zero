"""Focused original hero skills: both choices, real hand combination and per-turn restriction."""
import json
from pathlib import Path

ROOT = 'Canvas/BattleTable/'
steps = []
def step(path, label, expected=None, capture=False):
    steps.append(dict(buttonPath=ROOT+path, label=label, waitAfterSeconds=.3, skipScreenshot=not capture,
        after=[dict(key=k, expected=str(v), rule='原英雄技能、次数、独立元素贡献及实际抗性结算') for k,v in {'ui.truncated':'', **(expected or {})}.items()]))
loadout=0
encounter=0
rejects=0
for l, hero, pool, name in [(6, 'Arythea', 'hero_skill_pool_000', '炽热剑术'), (7, 'Tovak', 'hero_skill_pool_007', '寒冰剑术')]:
    for option in [0, 1]:
        e = 0 if option == 0 else 3
        (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp', '打开出战配置'))
        while loadout != l:
            loadout = (loadout+1)%8
            step('CampShade/Camp/Btn_Loadout', '选择已学技能快照', {'draftLoadout':loadout})
        while encounter != e:
            encounter=(encounter+1)%4
            step('CampShade/Camp/Btn_Encounter','选择原敌人',{'draftEncounter':encounter})
        step('CampShade/Camp/Btn_Start', '开始独立英雄战斗', {'hero.id':hero, 'fame':3, 'skills.ids':pool+':02', 'skills.used':'False', 'hand':3, 'phase':'Ranged'})
        step('Btn_Skills','查看原技能图', {'ui.skillArtCount':1})
        step('Skills/Btn_Skill_0','技能时机核对',{'ui.Heading':name, 'skills.used':'False'},True)
        step('Btn_Skills','返回实际手牌')
        step('Inspector/Btn_Next','推进格挡',{'phase':'Block'})
        step('Hand/Btn_Card_basic_card_016_0','选择决心')
        step('Inspector/Btn_Choice_strong','选择强化格挡')
        step('Inspector/Btn_Play','实际支付蓝晶',{'block':5,'blue':0,'hand':2})
        step('Enemy_0/Btn_Target','选择格挡目标')
        step('Inspector/Btn_Resolve','完整格挡',{'block':0})
        step('Inspector/Btn_Next','推进承伤',{'phase':'AssignDamage'})
        step('Inspector/Btn_Next','承伤并进入近战',{'phase':'Melee','wounds':0})
        step('Btn_Skills','打开已学技能')
        step('Skills/Btn_Skill_0','选择原技能',{'ui.skillArtCount':1})
        step('Inspector/Btn_SkillChoice_'+str(option),'选择普通或元素分支')
        step('Inspector/Btn_UseSkill','确认实际使用',{'melee':2,'skills.used':'True','hand':2,'fame':3},True)
        rejects+=1
        step('Inspector/Btn_UseSkill','同回合再次使用明确拒绝',{'melee':2,'skills.used':'True','rejections':rejects,'ui.Feedback':'该技能本回合已经使用'})
        step('Btn_Skills','返回原卡组合')
        if option == 0:
            step('Hand/Btn_Card_basic_card_000_0','选择实际横置原卡')
            step('Inspector/Btn_Sideways','横置补足普通攻击',{'melee':3,'played':2,'hand':1})
        else:
            step('Hand/Btn_Card_basic_card_021_0','选择原狂怒')
            step('Inspector/Btn_Play','普通攻击保留物理类型',{'melee':4,'played':2,'hand':1,'red':1})
        step('Enemy_0/Btn_Target','核对原敌人有效攻击', {'ui.TargetPreview':'所选 1 名敌人   护甲合计 3\n有效攻击 3（不足不会累积伤害）'}, option == 1)
        step('Inspector/Btn_Resolve','实际击杀及名望', {'kills':1,'fame':5 if option == 0 else 7,
            'ui.Feedback':f'攻击结算：有效 3 / 护甲 3\n击败 1，名望 +{2 if option == 0 else 4}。'})
        step('Inspector/Btn_Next','战末保留技能次数',{'phase':'End','won':'True','wounds':0,'skills.used':'True','fame':5 if option == 0 else 7},True)

request=dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity',captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=200,useRunSubfolder=False,steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding='utf-8')
print(len(steps),'steps /',sum(len(s['after']) for s in steps),'checks')
