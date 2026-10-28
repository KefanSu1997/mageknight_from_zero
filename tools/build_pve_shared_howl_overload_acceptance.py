"""Original Howl + Mana Overload through two legal heroes and one real battle.

Builds the same replayable setup used by build_pve_shared_mana_acceptance.py.
Run explicitly to write the automation configuration. --from N replays the
successful prefix silently and keeps assertions/screenshots only from step N.
No synthetic resources, direct state mutations, or new scene are introduced.
"""
import argparse
import json
from pathlib import Path

steps = []
RULE = "原英雄合法等级/技能；真实原牌、付款、中央实体及正式轮转/战斗消费者"
OVERLOAD = "hero_skill_pool_007:09"
HOWL = "hero_skill_pool_004:09"


def click(path, label, expected=None, capture=False, rule=RULE):
    checks = {"ui.truncated": "", **(expected or {})}
    steps.append(dict(
        buttonPath="Canvas/BattleTable/" + path,
        label=label,
        waitAfterSeconds=.3,
        skipScreenshot=not capture,
        after=[dict(key=key, expected=str(value), rule=rule) for key, value in checks.items()],
    ))


def shared():
    click("Btn_JourneyTools", "实际回合工具")
    click("JourneyTools/Btn_SharedSkills", "原互动技能与中央区")


def close_shared():
    click("SharedSkills/Btn_Back", "返回实际玩家")
    click("JourneyTools/Btn_CloseTools", "返回旅程")


# Same original-card picker prefix as the accepted shared-mana route.
(click('Btn_Menu', '打开战斗菜单'), click("Btn_Camp", "双英雄合法构筑"))
click("CampShade/Camp/Btn_Loadout", "原牌构筑")
for _ in range(2):
    click("CampShade/Picker/Btn_NextPage", "下一页")
click("CampShade/Picker/Btn_Select_39", "零部队，实际库存红蓝各1")
p = "CampShade/DeedPicker/"
click("CampShade/Camp/Btn_ChooseCards", "两英雄独立原牌")
click(p + "Btn_ClearDeeds", "清空构筑选择")
for card in ["000", "001", "007", "008"]:
    click(p + "Btn_ChooseDeed_basic_card_" + card, "选择既有原基础卡 " + card)
click(p + "Btn_DeedNext", "下一页原牌")
for card in ["014", "016", "021"]:
    click(p + "Btn_ChooseDeed_basic_card_" + card, "选择既有原基础卡 " + card)
click(p + "Btn_DoneDeeds", "确认独立原牌实例")
click("CampShade/Camp/Btn_SmallDeckHeroBuild", "原英雄及原技能")
h = "CampShade/HeroBuild/"
click(h + "Btn_Hero_Tovak", "Tovak原魔力霸主")
click(h + "Btn_Level_2", "等级2仅一个原技能")
click(h + "Btn_Skill_9", "Tovak魔力霸主007:09")
click(h + "Btn_AddCompanion", "保存第一位实际英雄")
click(h + "Btn_Hero_Wolfhawk", "Wolfhawk原群狼嚎叫")
click(h + "Btn_Skill_9", "Wolfhawk群狼嚎叫004:09")
click(h + "Btn_Begin", "开始合法双英雄旅程", {"journey.players": 2})
click("Tactic_day_1/Btn_Tactic_day_1", "Tovak先手战术")
click("Tactic_day_4/Btn_Tactic_day_4", "Wolfhawk后手战术")
click("Btn_BeginTurn", "Tovak实际第一回合", {
    "hero.id": "Tovak", "hero.level": 2, "hero.skills": 1,
    "units.count": 0, "red": 1, "tokens.red": 0,
})
shared()
click("SharedSkills/Btn_Own_0", "原魔力霸主")
click("SharedSkills/Btn_Color_Red", "选原规则允许的红色", capture=True)
click("SharedSkills/Btn_Confirm", "本人获得红标记，原技能带红色进入中央", {
    "tokens.red": 1, "red": 1, "shared.count": 1, "shared.ids": OVERLOAD,
}, rule="魔力霸主：本人得选色标记；中央颜色等待后续真实供能原牌，不提前加攻击")
close_shared()

# Actual base March and Swiftness consume no red mana, preserving the shared token.
click("Btn_JourneyHand", "Tovak实际手牌")
click("JourneyHand/Btn_Hand_0", "原行进基础移动2")
click("JourneyHand/Btn_OutsideChoice_base", "基础效果无魔力费用")
click("JourneyHand/Btn_PlayOutside", "实际打出行进", {"movement": 2, "shared.ids": OVERLOAD})
click("JourneyHand/Btn_Hand_1", "行进离手后选择实际敏捷")
click("JourneyHand/Btn_OutsideChoice_base", "原敏捷基础移动2")
click("JourneyHand/Btn_PlayOutside", "两张原牌实际累计移动4", {"movement": 4, "tokens.red": 1, "shared.ids": OVERLOAD})
click("JourneyHand/Btn_CloseHand", "返回旅程")
click("Btn_JourneyMap", "实际地形消费者")
click("JourneyMap/Btn_Hex_forest", "白昼森林")
click("JourneyMap/Btn_Move", "支付移动3进入森林", {"movement": 1, "journey.position": "1,0", "shared.ids": OVERLOAD})
click("JourneyMap/Btn_CloseMap", "返回旅程")
click("Btn_EndTurn", "正式结束Tovak回合，中央标记保留", {"journey.completedTurns": 1, "shared.ids": OVERLOAD})
click("Btn_BeginTurn", "Wolfhawk实际回合及独立库存", {
    "hero.id": "Wolfhawk", "hero.level": 2, "hero.skills": 1,
    "command.free": 1, "units.count": 0, "red": 1, "blue": 1, "tokens.red": 0,
})
shared()
click("SharedSkills/Btn_Own_0", "原群狼嚎叫")
click("SharedSkills/Btn_Sideways_Move", "本次真实横置为移动")
click("SharedSkills/Btn_Card_0", "选择实际原行进材料", capture=True)
click("SharedSkills/Btn_Confirm", "横置4加一个空指挥标记等于移动5", {
    "movement": 5, "played": 1, "hand": 6, "command.free": 1,
    "shared.count": 2, "shared.ids": OVERLOAD + "," + HOWL,
}, rule="群狼嚎叫：选中原手牌进入已打出区，4+空指挥槽1=5；成功后发布实际技能")
close_shared()
click("Btn_JourneyMap", "Wolfhawk移动消费者")
click("JourneyMap/Btn_Hex_forest", "同一正式森林地形")
click("JourneyMap/Btn_Move", "实际支付3，保留移动2", {"movement": 2, "journey.position": "1,0"})
click("JourneyMap/Btn_CloseMap", "返回旅程")
click("Btn_JourneyEncounter_0", "正式兽人战斗强制领取中央群狼", {
    "phase": "Ranged", "source.enemies": "monster_032", "movement": 0,
    "shared.count": 1, "shared.ids": OVERLOAD,
    "enemies.armor": "3", "enemies.attacks": "4",
}, capture=True, rule="首位进入敌战的玩家包含技能主人；新行动丢弃旧移动，群狼先归还并强制选真实目标")
click("SharedSkills/Btn_ArmorEnemy_0", "同一原兽人护甲目标")
click("SharedSkills/Btn_Enemy_0", "同一原兽人实际攻击槽")
click("SharedSkills/Btn_Confirm", "群狼真实护甲3到2、攻击4到3", {
    "phase": "Ranged", "enemies.armor": "2", "enemies.attacks": "3", "shared.ids": OVERLOAD,
}, capture=True, rule="两个真实目标选择完成；本例同敌，护甲最低1、攻击最低0")

# Blue Determination is deliberately not eligible for the central red Overload.
click("Inspector/Btn_Next", "进入正式格挡阶段", {"phase": "Block"})
click("Hand/Btn_Card_basic_card_016_0", "原决心")
click("Inspector/Btn_Choice_strong", "原蓝费格挡5")
click("Inspector/Btn_Play", "实际支付蓝晶，不领取中央红标记", {"blue": 0, "block": 5, "shared.ids": OVERLOAD, "red": 1})
click("Enemy_0/Btn_Target", "选择群狼减攻后的兽人")
click("Inspector/Btn_Resolve", "真实格挡攻击3", {"block": 0, "wounds": 0})
click("Inspector/Btn_Next", "正式承伤阶段", {"phase": "AssignDamage"})
click("Inspector/Btn_Next", "确认零承伤并进入攻击阶段", {"phase": "Melee", "wounds": 0})
click("Hand/Btn_Card_basic_card_021_0", "原狂怒，不自制卡牌")
click("Inspector/Btn_Choice_strong", "原强化攻击4，真实红费")
click("Inspector/Btn_Play", "Wolfhawk红晶供能狂怒，自动领取Tovak红霸主并加4", {
    "red": 0, "tokens.red": 0, "melee": 8, "shared.count": 0, "shared.ids": "",
}, capture=True, rule="原狂怒强化红费攻击4；实际支付红色且产生攻击的本牌独立+4，总攻击8；跨玩家中央原token归还")
click("Enemy_0/Btn_Target", "选择护甲2的真实兽人")
click("Inspector/Btn_Resolve", "攻击8实际击杀护甲2兽人", {"kills": 1, "melee": 0, "fame": 5, "wounds": 0})
click("Inspector/Btn_Next", "正式战斗结束", {"phase": "End", "won": True, "shared.count": 0})
click("Inspector/Btn_EndTurn", "真实回合末弃牌与持续旅程归档", {
    "journey.completedTurns": 2, "journey.completedBattles": 1, "journey.turnActive": False,
    "shared.count": 0, "rejections": 0,
}, capture=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--from", dest="replay_from", type=int, default=0)
    args = parser.parse_args()
    if not 0 <= args.replay_from <= len(steps):
        parser.error("--from must be a valid zero-based step boundary")
    for step in steps[:args.replay_from]:
        step["after"] = []
        step["skipScreenshot"] = True
    config = dict(
        scenePath="Assets/Scenes/Part1/Part1_PveBattle.unity",
        captureInitialView=False, initialDelaySeconds=2,
        defaultWaitAfterSeconds=.3, maxRunSeconds=300,
        useRunSubfolder=False, steps=steps,
    )
    Path("AutomationConfigs/pve_battle.json").write_text(
        json.dumps(config, ensure_ascii=False, indent=2), encoding="utf-8")
    print(len(steps))


if __name__ == "__main__":
    main()
