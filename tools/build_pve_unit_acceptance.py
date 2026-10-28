"""Original unit commands in the playable PVE scene; no fixture-only mutation buttons."""
import json
from pathlib import Path

ROOT = "Canvas/BattleTable/"
steps = []
def step(path, label, expected=None, capture=False):
    values = {"ui.truncated": "", **(expected or {})}
    steps.append(dict(buttonPath=ROOT+path, label=label, waitAfterSeconds=.3,
        skipScreenshot=not capture, after=[dict(key=k, expected=str(v), rule="原部队费用、就绪与伤牌独立、物抗吸收及真实战斗结算") for k,v in values.items()]))
def next_phase(phase, expected=None, capture=False):
    step("Inspector/Btn_Next", "推进正式阶段_"+phase, {"phase":phase, **(expected or {})}, capture)
def target(i): step(f"Enemy_{i}/Btn_Target", "选择原敌人_"+str(i))
def unit(): step("Units/Btn_Unit_0", "选择实际自有部队")
def card(i): step(f"Hand/Btn_Card_basic_card_{i:03}_0", "选择原卡_"+str(i))

(step('Btn_Menu', '打开战斗菜单'), step("Btn_Camp", "出战配置"))
for i in (1,2): step("CampShade/Camp/Btn_Loadout", "选择带部队快照", {"draftLoadout":i})
for i in (1,2): step("CampShade/Camp/Btn_Encounter", "选择双重哨戒", {"draftEncounter":i})
step("CampShade/Camp/Btn_Start", "真实魔像与双哨卫", {"source.units":"army_2_001", "units.ready":"True", "units.wounds":0,
    "source.enemies":"monster_032,monster_032", "hand":3, "phase":"Ranged", "fame":0})
step("Btn_Roster", "查看原部队卡面", {"ui.unitArtCount":1}, True)
next_phase("Block")
unit()
step("Inspector/Btn_UnitAbility_2", "蓝色支付寒冰格挡4", {"blue":0,"block":4,"units.ready":"False","units.wounds":0}, True)
step("Inspector/Btn_UnitAbility_0", "耗竭后拒绝再激活", {"block":4,"units.ready":"False","rejections":1,"ui.Feedback":"部队必须就绪、未受伤且未销毁"})
target(0)
step("Inspector/Btn_Resolve", "魔像格挡第一个哨卫", {"block":0,"units.wounds":0})
next_phase("AssignDamage")
target(1); unit()
step("Inspector/Btn_AssignUnit", "耗竭魔像预留第二次攻击承伤", {"units.plan":"1:0:army_2_001","units.wounds":0,"wounds":0}, True)
next_phase("Melee", {"units.wounds":1,"units.ready":"False","wounds":0,"realWounds":0}, True)
unit()
step("Inspector/Btn_UnitAbility_0", "受伤部队不能再攻击", {"melee":0,"units.wounds":1,"rejections":2})
step("Btn_Roster", "返回原手牌", {"ui.cardArtCount":3})
card(21)
step("Inspector/Btn_Choice_strong", "强化狂怒")
step("Inspector/Btn_Play", "原狂怒红色付款", {"melee":4,"red":0,"played":1})
target(1)
step("Inspector/Btn_Resolve", "近战击败第二名", {"kills":1,"fame":2})
card(16)
step("Inspector/Btn_Play", "原决心基础攻击", {"melee":2,"played":2})
card(0)
step("Inspector/Btn_Sideways", "原卡横置补足攻击", {"melee":3,"played":3,"hand":0})
target(0)
step("Inspector/Btn_Resolve", "击败第一名", {"kills":2,"fame":4})
next_phase("End", {"won":"True","fame":4,"wounds":0,"units.wounds":1,"units.ready":"False"}, True)

(step('Btn_Menu', '打开战斗菜单'), step("Btn_Camp", "切换烈焰援军"))
step("CampShade/Camp/Btn_Loadout", "选择原烈焰枪手", {"draftLoadout":3})
step("CampShade/Camp/Btn_Encounter", "选择原毒素兽人", {"draftEncounter":3})
step("CampShade/Camp/Btn_Start", "第二套真实部队出战", {"source.units":"army_003","source.enemies":"monster_006","fame":0,"units.ready":"True"})
step("Btn_Roster", "查看烈焰枪手原卡", {"ui.unitArtCount":1})
unit()
step("Inspector/Btn_UnitAbility_1", "支付红色发动远程火焰6", {"red":0,"ranged":6,"units.ready":"False"}, True)
step("Inspector/Btn_UnitAbility_1", "拒绝同部队重复发动", {"ranged":6,"rejections":3})
target(0)
step("Inspector/Btn_Resolve", "火焰6不被物抗减半", {"ranged":0,"kills":1,"fame":4,
    "ui.Feedback":"攻击结算：有效 6 / 护甲 3\n击败 1，名望 +4。"}, True)
next_phase("Block"); next_phase("AssignDamage"); next_phase("Melee", {"wounds":0,"units.wounds":0})
next_phase("End", {"won":"True","fame":4,"units.ready":"False","units.wounds":0,"played":0,"hand":3}, True)

request=dict(scenePath="Assets/Scenes/Part1/Part1_PveBattle.unity",captureInitialView=False,initialDelaySeconds=2,
    defaultWaitAfterSeconds=.3,maxRunSeconds=150,useRunSubfolder=False,steps=steps)
Path("AutomationConfigs/pve_battle.json").write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding="utf-8")
print(f"{len(steps)} steps / {sum(len(s['after']) for s in steps)} checks")
