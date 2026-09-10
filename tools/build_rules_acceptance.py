"""Generate reviewable, fixed numerical expectations; never import the game implementation.

Run from the repository root: python tools/build_rules_acceptance.py
"""
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PREFIX = "Canvas/UIRoot/"
UI_KEYS = {
    "Combat": {"block", "attack", "effectiveBlock", "requiredBlock", "effectiveAttack", "wounds", "fame"},
    "Exploration": {"movement", "cost", "tiles", "tileDeck"},
    "Recruitment": {"influence", "units", "freeSlots", "ready", "cost"},
    "Journey": {"hand", "deck", "discard", "movement", "influence", "redCrystal", "units", "block", "attack", "wounds", "fame"},
}


class Scenario:
    def __init__(self, name):
        self.name = name
        self.steps = []

    def step(self, button, label, after, rule, before=None, area="Actions"):
        def expectations(values, visible=False):
            result = []
            for key, value in values.items():
                result.append({"key": key, "expected": str(value), "rule": rule})
                if visible and key in UI_KEYS[self.name]:
                    result.append({"key": "ui:" + key, "expected": str(value), "rule": "界面显示必须与规则结果一致；" + rule})
            return result
        self.steps.append({
            "buttonPath": PREFIX + (area + "/" if area else "") + "Btn_" + button,
            "label": label,
            "waitAfterSeconds": 0.15,
            "before": expectations(before or {}),
            "after": expectations(after, True),
        })

    def case(self, case, after):
        self.step("case_" + case, "案例_" + case, after, "切换训练案例重置该案例的全部状态。", area="Sidebar/Cases")

    def blocks(self, count, effective=None):
        for i in range(1, count + 1):
            self.step("blockPlus", f"分配格挡_{i}", {"block": i, "effectiveBlock": i if effective is None else i // 2},
                      "每次增加1点分配；物理格挡火焰半效向下取整。" if effective else "每次增加1点物理格挡。",
                      {"block": i - 1})

    def save(self):
        request = {
            "scenePath": f"Assets/Scenes/Part1/Part1_{self.name}Rules.unity",
            "screenshotsDirectory": f"AutomationOutputs/RulesScenarios/20260909/{self.name}/captures",
            "reportPath": f"AutomationOutputs/RulesScenarios/20260909/{self.name}/report.json",
            "useRunSubfolder": True, "captureInitialView": True, "initialDelaySeconds": 1.2,
            "defaultWaitAfterSeconds": 0.15, "steps": self.steps,
        }
        path = ROOT / "AutomationConfigs" / ("rules_" + self.name.lower() + ".json")
        path.write_text(json.dumps(request, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        return len(self.steps)


def combat():
    s = Scenario("Combat")
    s.blocks(3)
    s.step("resolve", "部分格挡不能减伤", {"wounds": 2, "fame": 3, "enemyAlive": 0, "resolved": 1},
           "格挡3<4，完整伤害4÷英雄护甲2=2伤口；攻击4达到护甲4，名望+3。", {"wounds": 0, "fame": 0})
    s.step("resolve", "禁止重复领取名望", {"accepted": 0, "wounds": 2, "fame": 3}, "已结算敌人不得重复发奖。")
    s.case("normal", {"block": 0, "wounds": 0, "fame": 0})
    s.blocks(4)
    s.step("resolve", "足额格挡无伤击败", {"wounds": 0, "fame": 3, "enemyAlive": 0}, "格挡4≥4完全格挡；攻击4≥护甲4。")
    s.case("swift", {"requiredBlock": 8, "block": 0, "wounds": 0})
    s.blocks(4)
    s.step("resolve", "迅捷需要双倍格挡", {"wounds": 2, "fame": 3}, "迅捷需格挡4×2=8；格挡4不足，承受伤害4并得到2伤口。")
    s.case("brutal", {"requiredBlock": 4, "block": 0, "wounds": 0})
    s.blocks(3)
    s.step("resolve", "残暴伤害翻倍", {"wounds": 4, "fame": 3}, "格挡3不足；残暴伤害4×2=8，8÷护甲2=4伤口。")
    s.case("fire", {"requiredBlock": 4, "block": 0, "wounds": 0})
    s.blocks(4, effective=True)
    s.step("attackFire", "选择火焰攻击", {"effectiveAttack": 2}, "火焰攻击4遇火抗减半为2。")
    s.step("resolve", "错误元素造成受伤且敌人存活", {"wounds": 2, "fame": 0, "enemyAlive": 1},
           "物理格挡4对火攻击仅有效2，承受完整伤害4；火攻击4遇火抗有效2<护甲4。")
    s.case("fire", {"block": 0, "wounds": 0, "fame": 0})
    s.blocks(4, effective=True)
    s.step("blockIce", "改用冰格挡", {"effectiveBlock": 4, "effectiveAttack": 4}, "冰格挡火攻击全效；物理攻击不受火抗影响。")
    s.step("resolve", "正确元素无伤击败", {"wounds": 0, "fame": 3, "enemyAlive": 0}, "有效格挡4达到4，物理攻击4达到护甲4。")
    return s


def exploration():
    s = Scenario("Exploration")
    s.step("move", "白昼进入森林", {"movement": 3, "positionQ": 1, "positionR": 0}, "森林白天费用3；6−3=3。", {"movement": 6, "cost": 3})
    s.step("select_frontier", "选择未知空位", {"movement": 3, "cost": 2}, "选中地块不消耗资源。", area="Map")
    s.step("explore", "揭示后棋子留在原位", {"movement": 1, "tiles": 7, "tileDeck": 0, "positionQ": 1, "revealed": 1},
           "探索另付2，3−2=1；地块数6→7，牌堆1→0，玩家不移动。", {"tiles": 6, "tileDeck": 1})
    s.step("move", "移动力不足保留位置", {"accepted": 0, "movement": 1, "positionQ": 1}, "进入平原需2，剩余1；失败不扣费，不移动。")
    s.step("explore", "空牌堆与重复探索被拒绝", {"accepted": 0, "movement": 1, "tiles": 7, "tileDeck": 0}, "已揭示地块不能重复探索，空牌堆不能取牌。")
    s.step("night", "重置为夜间案例", {"movement": 6, "day": 0, "cost": 5}, "夜间森林费用5。", area="Sidebar/Cases")
    s.step("move", "夜间森林费用增加", {"movement": 1, "positionQ": 1}, "6−5=1；夜间森林不是更便宜。")
    s.step("day", "重置为白昼案例", {"movement": 6, "positionQ": 0}, "开始独立白昼案例。", area="Sidebar/Cases")
    s.step("select_lake", "选择湖泊", {"cost": "不可通行", "movement": 6}, "湖泊没有普通移动费用，不能进入。", area="Map")
    s.step("move", "湖泊不能进入", {"accepted": 0, "positionQ": 0, "positionR": 0, "movement": 6}, "不可通行地形拒绝移动并保留资源。")
    s.step("select_frontier", "选择不相邻空位", {"movement": 6}, "此时距离2。", area="Map")
    s.step("explore", "禁止远距离探索", {"accepted": 0, "movement": 6, "tiles": 6, "tileDeck": 1}, "只可探索玩家相邻空位，失败不抽地块。")
    s.step("select_desert", "白昼沙漠预览", {"cost": 5}, "沙漠白天费用5。", area="Map")
    s.step("move", "进入白昼沙漠", {"movement": 1, "positionQ": 1, "positionR": -1}, "6−5=1。")
    return s


def recruitment():
    s = Scenario("Recruitment")
    s.step("recruit", "影响力不足不扣费", {"accepted": 0, "influence": 4, "units": 0, "offer": 3}, "守卫费用5，现有4；失败时影响力与招募列不变。")
    s.step("select_2", "选择修道院单位", {"selected": 2}, "选择单位不扣费。", area="")
    s.step("recruit", "村庄不能招募修道院单位", {"accepted": 0, "influence": 4, "units": 0}, "地点图标必须匹配。")
    s.step("select_0", "选回守卫", {"selected": 0}, "守卫允许村庄招募。", area="")
    s.step("influence", "打出影响力2", {"influence": 6}, "交涉牌提供2，4+2=6。", {"influence": 4})
    s.step("influence", "同一张牌不能重复用", {"accepted": 0, "influence": 6}, "交涉牌已使用，资源保持6。")
    s.step("recruit", "守卫就绪加入", {"influence": 1, "units": 1, "freeSlots": 0, "ready": 1, "offer": 2},
           "6−5=1；部队0→1、初始指挥槽1被占用、单位就绪、公开列3→2。")
    s.step("recruit", "同一单位不能重复招募", {"accepted": 0, "influence": 1, "units": 1, "offer": 2}, "已离开公开列的单位不可再次购买。")
    s.step("activate", "使用部队后横置", {"ready": 0, "units": 1}, "部队使用后不再就绪，但仍占用指挥槽。")
    s.step("activate", "横置单位不能重复使用", {"accepted": 0, "ready": 0, "units": 1}, "本昼夜没有就绪单位。")
    s.case("reputation", {"influence": 12, "units": 0, "freeSlots": 2, "reputationBonus": 2})
    s.step("recruit", "声望修正只在开始应用", {"influence": 7, "units": 1}, "基础影响10+声望修正2=12，招募守卫扣5后余7。")
    s.step("select_1", "选择游侠", {"selected": 1, "cost": 7}, "游侠牌面费用7。", area="")
    s.step("recruit", "第二次招募不重复声望优惠", {"influence": 0, "units": 2, "freeSlots": 0}, "7−7=0，不能再次追加声望修正2。")
    s.case("capacity", {"influence": 12, "units": 0, "freeSlots": 1})
    s.step("recruit", "占满唯一指挥槽", {"influence": 7, "units": 1, "freeSlots": 0}, "等级1只有1个指挥槽；12−5=7。")
    s.step("select_1", "资源足够但指挥槽不足", {"selected": 1, "cost": 7}, "费用7与现有影响力7相等。", area="")
    s.step("recruit", "缺少指挥槽不得扣费", {"accepted": 0, "influence": 7, "units": 1, "freeSlots": 0, "offer": 2}, "虽然影响力足够，指挥槽已满，失败时不扣费用或移除公开牌。")
    s.case("monastery", {"influence": 6, "units": 0, "location": "Monastery"})
    s.step("select_2", "在修道院选择学徒", {"selected": 2, "cost": 6}, "修道院图标匹配。", area="")
    s.step("recruit", "修道院招募成功", {"influence": 0, "units": 1, "ready": 1}, "6−6=0，单位就绪加入。")
    return s


def journey():
    s = Scenario("Journey")
    s.step("recruit", "营地不能直接招募", {"accepted": 0, "units": 0, "hand": 5}, "尚未进入村庄和开始交涉，拒绝招募。")
    s.step("march", "打出行军牌", {"movement": 6, "hand": 4, "played": 1}, "训练行军牌提供6移动力；卡牌转入已打出区。")
    s.step("explore", "支付2揭示村庄", {"movement": 4, "revealed": 1, "positionQ": 0}, "6−2=4，揭示地块不会自动移动。")
    s.step("travel", "另付2进入平原村庄", {"movement": 2, "positionQ": 1}, "村庄底层地形为平原；4−2=2。")
    s.step("talk", "打出交涉牌", {"influence": 6, "hand": 3, "played": 2}, "训练交涉牌提供6影响力，开始互动。")
    s.step("march", "交涉后不可再移动", {"accepted": 0, "movement": 2, "hand": 3}, "行动阶段开始后不能回到移动阶段。")
    s.step("recruit", "招募一名守卫", {"influence": 1, "units": 1, "ready": 1}, "费用5，6−5=1；守卫就绪加入。")
    s.step("endTurn", "弃牌补牌与资源保留", {"turn": 2, "hand": 5, "deck": 9, "discard": 2, "played": 0,
           "movement": 0, "influence": 0, "redCrystal": 1, "units": 1},
           "两张已打出牌弃置，补2张至手牌5；牌库11−2=9。回合资源清零，水晶与部队保留。")
    s.step("unit", "守卫提供格挡3", {"block": 3, "ready": 0}, "守卫发动训练格挡能力3，横置一次。")
    s.step("unit", "守卫不能重复提供格挡", {"accepted": 0, "block": 3, "ready": 0}, "横置单位不可再次使用。")
    s.step("boost", "消耗红晶强化攻击", {"attack": 6, "redCrystal": 0, "hand": 4, "played": 1}, "支付红1，训练猛击由4强化为6，手牌减1。", {"redCrystal": 1, "hand": 5})
    s.step("boost", "同一攻击牌不可重复强化", {"accepted": 0, "attack": 6, "redCrystal": 0, "hand": 4}, "攻击牌已使用，不能重复产生攻击或扣牌。")
    s.step("battle", "无伤清剿并获得名望", {"wounds": 0, "fame": 3, "completed": 1}, "格挡3达到敌攻击3，攻击6≥敌护甲5，无伤获名望3。", {"fame": 0})
    s.step("battle", "完成后不能重复领奖", {"accepted": 0, "wounds": 0, "fame": 3, "completed": 1}, "同一敌人只发放一次奖励。")
    return s


def expected_failure():
    """Deliberately wrong oracle: report must be failed, proving checks are enforced."""
    request = {
        "scenePath": "Assets/Scenes/Part1/Part1_CombatRules.unity",
        "screenshotsDirectory": "AutomationOutputs/RulesScenarios/20260909/ExpectedFailure/captures",
        "reportPath": "AutomationOutputs/RulesScenarios/20260909/ExpectedFailure/report.json",
        "initialDelaySeconds": 1, "captureInitialView": False,
        "steps": [{"buttonPath": "Canvas/UIRoot/Actions/Btn_blockPlus", "label": "故意错误预期必须失败",
                   "after": [{"key": "block", "expected": "999", "rule": "故障注入：点击一次实际只能产生1，验收不得误报通过。"}]}],
    }
    (ROOT / "AutomationConfigs/rules_expected_failure.json").write_text(json.dumps(request, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    counts = {scenario.name: scenario.save() for scenario in [combat(), exploration(), recruitment(), journey()]}
    expected_failure()
    print(json.dumps(counts))
