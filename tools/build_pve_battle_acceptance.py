"""Focused pointer-driven acceptance; expectations are independent printed-rule values.
Run from project root: python tools/build_pve_battle_acceptance.py
"""
import json
import sys
from pathlib import Path

ROOT = "Canvas/BattleTable/"
SIDE = "Inspector/"
steps = []

def check(values):
    return [{"key": key, "expected": str(value), "rule": "原卡费用/敌人印刷数值与正式战斗阶段"}
            for key, value in values.items()]

def step(button, label, values=None, capture=False):
    expected = {"ui.truncated": "", **(values or {})}
    steps.append(dict(buttonPath=ROOT + button, label=label, after=check(expected),
                      skipScreenshot=not capture, waitAfterSeconds=0.3))

def card(number):
    step(f"Hand/Btn_Card_basic_card_{number:03}_0", "选择原卡_" + str(number))

def strong():
    step(SIDE + "Btn_Choice_strong", "选择印刷强化")

def play(values, capture=False):
    step(SIDE + "Btn_Play", "确认原卡与费用", values, capture)

def target(index):
    step(f"Enemy_{index}/Btn_Target", "选择实际敌人_" + str(index))

def advance(values, capture=False):
    step(SIDE + "Btn_Next", "推进正式阶段", values, capture)

def status(fame, wounds, red, blue, white):
    return f"护甲 2    名望 {fame}    创伤 {wounds}\n晶体   红 {red}   蓝 {blue}   白 {white}"

advance({"phase": "Block", "hand":5,"played":0,"fame":0,"wounds":0,
         "source.cards":"basic_card_016,basic_card_021,basic_card_000,basic_card_014,basic_card_008",
         "source.enemies":"monster_032","art.cards":"True","art.enemies":"True",
         "ui.cardArtCount":5,"ui.enemyArtCount":1,"ui.status":status(0,0,1,1,0)}, True)
card(16); strong()
play({"block":5,"blue":0,"hand":4,"played":1,"ui.status":status(0,0,1,0,0)}, True)
target(0)
step(SIDE+"Btn_Resolve","格挡5对攻击4",{"block":0,"wounds":0,"phase":"Block"})
advance({"phase":"AssignDamage","wounds":0})
advance({"phase":"Melee","wounds":0,"realWounds":0,"discardWounds":0})
card(21); strong()
play({"melee":4,"red":0,"hand":3,"played":2,"ui.status":status(0,0,0,0,0)})
target(0)
step(SIDE+"Btn_Resolve","攻击4击败护甲3",{"kills":1,"fame":2,"melee":0,"wounds":0})
advance({"phase":"End","won":"True","kills":1,"fame":2,"hand":3,"played":2,
         "ui.status":status(2,0,0,0,0),"ui.Heading":"战斗胜利"}, True)

(step('Btn_Menu', '打开战斗菜单'), step("Btn_Camp","打开独立出战配置"))
step("CampShade/Camp/Btn_Loadout","只更换手牌",{"draftLoadout":1,"draftEncounter":0})
step("CampShade/Camp/Btn_Encounter","独立更换敌群",{"draftLoadout":1,"draftEncounter":1})
step("CampShade/Camp/Btn_Start","同场景重建第二场",{"loadout":1,"encounter":1,"phase":"Ranged","won":"False",
    "hand":5,"played":0,"fame":0,"wounds":0,"kills":0,"source.cards":"basic_card_007,basic_card_011,basic_card_021,basic_card_016,basic_card_000",
    "source.enemies":"monster_006,monster_007","ui.cardArtCount":5,"ui.enemyArtCount":2,"art.cards":"True","art.enemies":"True"},True)
card(7)
play({"ranged":3,"white":0,"hand":4,"played":1,"ui.status":status(0,0,1,2,0)})
card(11); strong()
play({"ranged":6,"blue":1,"hand":3,"played":2,"ui.status":status(0,0,1,1,0)})
target(0)
step(SIDE+"Btn_Resolve","物抗令6减半为3击杀且利斧奖励1",{"kills":1,"fame":5,"wounds":0,"ranged":0,"ui.status":status(5,0,1,1,0),
    "ui.Feedback":"攻击结算：有效 3 / 护甲 3\n击败 1，名望 +5。"},True)
advance({"phase":"Block","kills":1,"fame":5})
step(SIDE+"Btn_Rules","按需展开格挡规则",{"ui.Rules":"逐次完整格挡；迅捷需要双倍。对元素攻击的无效格挡按正式规则减半。"})
card(16); strong()
play({"block":5,"blue":0,"hand":2,"played":3})
target(1)
step(SIDE+"Btn_Resolve","迅捷6拒绝格挡5且资源保留",{"rejections":1,"block":5,"hand":2,"played":3,
    "ui.Feedback":"格挡不足：有效 5 / 需要 6；部分格挡不能减伤"},True)
advance({"phase":"AssignDamage","wounds":0})
advance({"phase":"Melee","wounds":2,"realWounds":2,"discardWounds":0,"hand":4,"block":0,
    "ui.status":status(5,2,1,0,0),"ui.cardArtCount":2},True)
card(21); strong()
play({"melee":4,"red":0,"hand":3,"played":4})
target(1)
step(SIDE+"Btn_Resolve","狂怒击败狼骑兵",{"kills":2,"fame":8,"melee":0,"wounds":2,"realWounds":2})
advance({"phase":"End","won":"True","kills":2,"fame":8,"hand":3,"played":4,"wounds":2,"realWounds":2,
    "discardWounds":0,"ui.status":status(8,2,0,0,0),"ui.Heading":"战斗胜利"},True)

# Guard encounter already passed: only the original enemy correction and its consumer are rerun.
warband_only = "--warband-only" in sys.argv
if warband_only:
    steps = steps[14:]

request = dict(scenePath="Assets/Scenes/Part1/Part1_PveBattle.unity", captureInitialView=not warband_only,
               initialDelaySeconds=2,defaultWaitAfterSeconds=0.3,maxRunSeconds=150,
               useRunSubfolder=False,steps=steps)
Path("AutomationConfigs/pve_battle.json").write_text(json.dumps(request,ensure_ascii=False,indent=2),encoding="utf-8")
print(f"{len(steps)} pointer steps, {sum(len(s['after']) for s in steps)} independent checks")
