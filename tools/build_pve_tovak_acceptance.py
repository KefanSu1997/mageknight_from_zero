"""Seven original Tovak skill battles; backend evidence is required before this UI suite."""
import json
import sys
from pathlib import Path

steps = []
loadout = encounter = rejects = 0
skills = False

def step(path, label, expected=None, capture=False):
    steps.append(dict(buttonPath='Canvas/BattleTable/' + path, label=label, waitAfterSeconds=.3,
        skipScreenshot=not capture, after=[dict(key=k, expected=str(v), rule='原技能、昼夜、独立元素、实际目标和战斗结算')
        for k,v in {'ui.truncated':'', **(expected or {})}.items()]))

def start(l, e, index, day='Day'):
    global loadout, encounter, skills
    (step('Btn_Menu', '打开战斗菜单'), step('Btn_Camp', '选择独立技能战斗'))
    while loadout != l:
        loadout = (loadout + 1) % 13
        step('CampShade/Camp/Btn_Loadout', '选择已学原技能', {'draftLoadout':loadout})
    while encounter != e:
        encounter = (encounter + 1) % 8
        step('CampShade/Camp/Btn_Encounter', '选择原敌人与昼夜', {'draftEncounter':encounter})
    step('CampShade/Camp/Btn_Start', '开始原技能独立快照', {'phase':'Ranged', 'hand':4, 'fame':3,
        'red':1, 'blue':1, 'hero.id':'Tovak', 'skills.ids':f'hero_skill_pool_007:{index:02}',
        'skills.used':'False', 'dayPart':day, 'art.cards':'True', 'art.enemies':'True'})
    skills = False

def show_skills(want):
    global skills
    if skills != want:
        step('Btn_Skills', '查看原技能图' if want else '返回实际手牌')
        skills = want

def skill():
    show_skills(True)
    step('Skills/Btn_Skill_0', '选择已学技能', {'ui.skillArtCount':1})

def play(n, choice, expected=None):
    show_skills(False)
    step(f'Hand/Btn_Card_basic_card_{n:03}_0', '选择实际原卡')
    step('Inspector/Btn_Choice_' + choice, '选择原卡效果')
    step('Inspector/Btn_Play', '正式出牌', expected)

def sideways(expected):
    show_skills(False)
    step('Hand/Btn_Card_basic_card_000_0', '选择实际横置牌')
    step('Inspector/Btn_Sideways', '横置原卡', expected)

def advance(phase, extra=None, capture=False):
    step('Inspector/Btn_Next', '进入_' + phase, {'phase':phase, **(extra or {})}, capture)

def target(): step('Enemy_0/Btn_Target', '选择实际存活目标')
def block(effective, need):
    target()
    step('Inspector/Btn_Resolve', '真实格挡成立', {'block':0,
        'ui.Feedback':f'格挡成立：有效 {effective} ≥ {need}。效果随承伤阶段结算。'})

def kill(effective, armor, fame, gain):
    target()
    step('Inspector/Btn_Resolve', '实际攻击与击杀奖励', {'kills':1, 'fame':fame,
        'ui.Feedback':f'攻击结算：有效 {effective} / 护甲 {armor}\n击败 1，名望 +{gain}。'})

weakening_only = '--weakening-only' in sys.argv
for night in (() if weakening_only else (False, True)):
    start(10, 5 if night else 0, 1, 'Night' if night else 'Day')
    skill()
    step('Inspector/Btn_UseSkill', '黑夜狙击按实际昼夜取值', {'ranged':2 if night else 1, 'skills.used':'True'}, True)
    rejects += 1
    step('Inspector/Btn_UseSkill', '同回合重复使用被拒绝', {'ranged':2 if night else 1, 'rejections':rejects})
    play(11, 'base' if night else 'strong', {'ranged':3 if night else 4, 'blue':1 if night else 0, 'hand':3})
    kill(3 if night else 4, 3, 5 if night else 6, 2 if night else 3)
    advance('Block'); advance('AssignDamage'); advance('Melee', {'wounds':0, 'realWounds':0})
    advance('End', {'won':'True', 'fame':5 if night else 6}, True)

for option, e in (() if weakening_only else ((0,0), (1,6), (2,7))):
    start(11, e, 3)
    advance('Block'); skill()
    step('Inspector/Btn_SkillChoice_' + str(option), '选择原印刷格挡元素')
    step('Inspector/Btn_UseSkill', '盾牌精通独立元素贡献', {'block':3 if option == 0 else 2, 'skills.used':'True'}, True)
    if option == 0: sideways({'block':4})
    if option == 1: play(16, 'block', {'block':4})
    block(4 if option == 0 else 3 if option == 1 else 2, 4 if option == 0 else 3 if option == 1 else 2)
    advance('AssignDamage'); advance('Melee', {'wounds':0, 'realWounds':0})
    play(21, 'strong', {'melee':4, 'red':0})
    if option == 1:
        play(11, 'strong', {'ranged':3, 'blue':0}); sideways({'melee':5})
    if option == 2:
        play(16, 'attack', {'melee':6}); play(11, 'base', {'ranged':1}); sideways({'melee':7})
    kill(4, 3 if option == 0 else 4, 5 if option == 0 else 9 if option == 1 else 8,
        2 if option == 0 else 6 if option == 1 else 5)
    advance('End', {'won':'True', 'wounds':0}, True)

for e, original_armor, fame in ((3,3,7), (7,4,8)):
    start(12, e, 4); skill()
    rejects += 1
    step('Inspector/Btn_UseSkill', '缺少目标不消耗使用次数', {'skills.used':'False', 'rejections':rejects, 'enemies.armor':original_armor})
    target()
    step('Inspector/Btn_UseSkill', '按敌人实际抗性降低护甲', {'skills.used':'True', 'enemies.armor':2}, True)
    advance('Block'); play(16, 'strong', {'block':5, 'blue':0})
    block(5 if e == 3 else 2, 4 if e == 3 else 2)
    advance('AssignDamage'); advance('Melee', {'wounds':0, 'realWounds':0})
    play(21, 'strong', {'melee':4, 'red':0})
    kill(2, 2, fame, fame-3)
    advance('End', {'won':'True', 'enemies.armor':original_armor, 'skills.used':'True'}, True)

request = dict(scenePath='Assets/Scenes/Part1/Part1_PveBattle.unity', captureInitialView=False,
    initialDelaySeconds=2, defaultWaitAfterSeconds=.3, maxRunSeconds=300, useRunSubfolder=False, steps=steps)
Path('AutomationConfigs/pve_battle.json').write_text(json.dumps(request, ensure_ascii=False, indent=2), encoding='utf-8')
print(len(steps), 'steps /', sum(len(s['after']) for s in steps), 'checks')
