"""Validate saved Unity evidence and produce a readable acceptance ledger.

Run after the four scenes, EditMode tests, and two final Console checks:
    python tools/summarize_rules_acceptance.py
This reads independent configuration expectations; it never imports game code.
"""
import hashlib
import json
import struct
from datetime import datetime
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / "AutomationOutputs/RulesScenarios/20260909"
NAMES = {"Combat": "林间交锋 · 战斗", "Exploration": "迷雾边境 · 探索",
         "Recruitment": "村庄盟约 · 招募", "Journey": "远征初章 · 两回合流程"}
LABELS = {
    "accepted": "操作被接受", "revision": "操作序号", "case": "案例",
    "block": "格挡", "attack": "攻击", "effectiveBlock": "有效格挡",
    "requiredBlock": "格挡需求", "effectiveAttack": "有效攻击", "wounds": "伤口",
    "fame": "名望", "enemyAlive": "敌人存活", "resolved": "已结算",
    "movement": "移动力", "cost": "所选费用", "tiles": "已揭示地块",
    "tileDeck": "待探索地块", "positionQ": "位置Q", "positionR": "位置R",
    "influence": "影响力", "units": "部队", "freeSlots": "空闲指挥槽",
    "slots": "指挥槽", "ready": "就绪部队", "reputationBonus": "声望修正",
    "hand": "手牌", "deck": "牌库", "discard": "弃牌", "played": "已打出",
    "turn": "回合", "redCrystal": "红色水晶", "redToken": "红色魔力",
    "revealed": "村庄已揭示", "completed": "任务完成", "battleDone": "战斗已结算",
}


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def link(label, path):
    return f"[{label}](<{Path(path).resolve().as_posix()}>)"


def safe(value):
    return str(value).replace("|", "\\|").replace("\n", " ")


def validate_scene(name):
    config = read(ROOT / f"AutomationConfigs/rules_{name.lower()}.json")
    report = read(BASE / name / "report.json")
    assert report["status"] == "success", name
    assert report["scenePath"] == config["scenePath"], name
    assert len(report["steps"]) == len(config["steps"]) + 1, name
    checked = 0
    for step in report["steps"]:
        assert step["success"], (name, step["label"])
        header = Path(step["screenshotPath"]).read_bytes()[:24]
        assert header[:8] == b"\x89PNG\r\n\x1a\n", step["screenshotPath"]
        assert struct.unpack(">II", header[16:24]) == (1920, 1080), step["screenshotPath"]
    for expected_step, step in zip(config["steps"], report["steps"][1:]):
        assert expected_step["label"] == step["label"], name
        assert expected_step["buttonPath"] == step["buttonPath"], name
        assert step["hitObject"] and step["runtimeRule"], (name, step["label"])
        expected_assertions = [(phase, a) for phase in ("before", "after")
                               for a in expected_step.get(phase, [])]
        assert len(expected_assertions) == len(step["assertions"]), step["label"]
        for (phase, expectation), actual in zip(expected_assertions, step["assertions"]):
            state = {s["key"]: s["value"] for s in step[phase + "State"]}
            assert actual["phase"] == phase, actual
            assert actual["key"] == expectation["key"], actual
            assert actual["expected"] == expectation["expected"], actual
            assert actual["actual"] == state[actual["key"]] == expectation["expected"], actual
            assert actual["passed"], actual
            checked += 1
    return report, checked


def detail(name, report, count):
    lines = [f"# {NAMES[name]}：逐步验收", "",
             f"{len(report['steps']) - 1} 次操作，{count} 条断言，全部通过。",
             "", "数值来自本次 Unity 运行记录；`ui:` 字段来自界面实际 TMP 文本。",
             "`操作被接受=0` 表示按规则拒绝操作；只要符合独立预期，这一步验收仍通过。", "",
             link("原始 JSON 报告（含完整前后状态和命中坐标）", BASE / name / "report.json"), "",
             link("初始截图", report["steps"][0]["screenshotPath"]), ""]
    for index, step in enumerate(report["steps"][1:], 1):
        lines += [f"## {index:02d} · {step['label']}", "",
                  f"操作：`{step['buttonPath']}`；命中 `{step['hitObject']}`，"
                  f"屏幕坐标 ({step['clickX']:g}, {step['clickY']:g})。", "",
                  "规则反馈：" + step["runtimeRule"], "",
                  "| 检查时点 | 字段 | 点击前 | 独立预期 | 实际 | 结果 |",
                  "| --- | --- | --- | --- | --- | --- |"]
        before = {s["key"]: s["value"] for s in step["beforeState"]}
        for a in step["assertions"]:
            key = a["key"]
            label = ("界面·" if key.startswith("ui:") else "") + LABELS.get(key.removeprefix("ui:"), key)
            lines.append(f"| {'点击前' if a['phase'] == 'before' else '点击后'} | {safe(label)} | "
                         f"{safe(before.get(key, '—'))} | {safe(a['expected'])} | {safe(a['actual'])} | 通过 |")
        lines += ["", link("本步骤截图", step["screenshotPath"]), ""]
    (BASE / f"{name}_acceptance.md").write_text("\n".join(lines), encoding="utf-8")


def main():
    scenes = {}
    for name in NAMES:
        report, count = validate_scene(name)
        detail(name, report, count)
        scenes[name] = {"operations": len(report["steps"]) - 1,
                        "screenshots": len(report["steps"]), "assertions": count,
                        "finishedAt": report["finishedAt"], "status": report["status"]}
    tests = read(BASE / "editmode-summary.json")
    assert tests["passed"] > 0 and tests["failed"] == 0 and tests["skipped"] == 0, tests
    consoles = [read(BASE / f"console_final_{i}.json") for i in (1, 2)]
    assert all(c["result"]["success"] and c["result"]["data"] == [] for c in consoles)
    probe = read(BASE / "ExpectedFailure/report.json")
    assert probe["status"] == "failed"
    assert any(a["expected"] == "999" and a["actual"] == "1" and not a["passed"]
               for step in probe["steps"] for a in step["assertions"])
    summary = {"generatedAt": datetime.now().astimezone().isoformat(), "scenes": scenes,
               "operations": sum(s["operations"] for s in scenes.values()),
               "screenshots": sum(s["screenshots"] for s in scenes.values()),
               "assertions": sum(s["assertions"] for s in scenes.values()),
               "editMode": tests, "consoleErrors": [0, 0], "wrongExpectationRejected": True}
    evidence = sorted(BASE.glob("*/report.json")) + sorted(BASE.glob("console_final_*.json"))
    evidence += [BASE / "editmode-summary.json", BASE / "editmode-results.xml"]
    evidence += sorted((ROOT / "Assets/Resources/UI/RulesScenarios").glob("*.png"))
    summary["sha256"] = {p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
                         for p in evidence}
    (BASE / "acceptance_summary.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    lines = ["# 四个规则场景：验收总览", "",
             f"Unity 6000.6.0f1；{summary['operations']} 次操作、{summary['screenshots']} 张正式截图、"
             f"{summary['assertions']} 条数值/规则断言通过。EditMode {tests['passed']} 项通过，0 失败、0 跳过。",
             "最终 Console 按 types=[error]、count=1000、includeStacktrace=true 连续两次返回 0 条。", "",
             "| 场景 | 可体验的规则与数值 | 操作 / 断言 | 逐步证据 |",
             "| --- | --- | --- | --- |"]
    features = {"Combat": "格挡3<攻击4→2伤口；迅捷需求8；残暴伤害翻倍；元素效率与抗性",
                "Exploration": "白昼森林6→3；探索3→1且留原地；夜间森林费用5；不可通行与非相邻拒绝",
                "Recruitment": "影响4不足以付5；交涉4→6，招募6→1；地点限制、一次声望修正、指挥槽、单位已用",
                "Journey": "两回合；手牌5/牌库11→打2张→弃2补2；红晶1→0，格挡3/攻击6→无伤名望3"}
    for name, title in NAMES.items():
        s = scenes[name]
        lines.append(f"| {title} | {features[name]} | {s['operations']} / {s['assertions']} | "
                     f"{link('操作→规则→预期→实际→截图', BASE / (name + '_acceptance.md'))} |")
    lines += ["", "## 如何打开和重跑", "",
              "四个场景都在 `Assets/Scenes/Part1/Part1_*Rules.unity`。双击场景后点击 Play；顶部四个标签可在编辑器运行时切换，左下可重置。",
              "Unity 菜单 `Tools > Mage Knight > Rules > Run All Four` 运行全部验收；`Run All EditMode Tests` 执行全量逻辑测试。",
              "生成独立预期：`python tools/build_rules_acceptance.py`。运行后汇总：`python tools/summarize_rules_acceptance.py`。", "",
              "## 验收为什么可信", "",
              "预期数值在 AutomationConfigs 中固定定义，生成脚本不导入游戏逻辑。运行器先校验点击前状态、按钮可交互和射线命中，再发送 Pointer 事件，核对点击后逻辑状态与实际 TMP 文本，保存截图。",
              "汇总脚本另行比对配置、报告、状态、断言完整性、截图 PNG 尺寸和 Console。运行器遇到异常、错误或超时会写失败结果并停止队列，旧报告不会冒充新运行成功。",
              "专门把格挡预期写为999，实测值1，验收器正确判失败；该故障注入报告不计入四场景通过数。", "",
              link("故障注入报告", BASE / "ExpectedFailure/report.json"), "",
              "## 实际鼠标与画面检查", "",
              "2026-09-09 22:30—22:36，在原生 Unity 窗口点击顶部切换、战斗格挡/结算、探索选择/支付、招募失败/补充影响力/招募成功，以及完整两回合任务。观察到的数值与自动化结果一致。",
              "人工逐图检查四个最终场景的插画、文字、地图节点、数值区和操作区；修复过标题截字、半透明描边污染、六边形 CanvasRenderer 缺失和地图边缘过近问题。", ""]
    for p in sorted((BASE / "manual").glob("*.jpg")):
        lines += ["- " + link(p.stem, p)]
    lines += ["", "## 范围与限制", "",
              "这是四个固定规则教学场景。训练卡、敌人和单位数值明确写在界面；两回合任务使用简化卡牌计数，不代表正式卡牌库全部能力已经实现。地图使用一格代表一个位置的教学布局，没有完整七格拼版、全部地形能力及随机冒险内容。",
              "本次通过的是 Windows Unity 编辑器中1920×1080的演示及现有EditMode测试；未制作手机/Player安装包，顶部跨场景导航当前为编辑器演示入口。",
              "现有工作分支仍保留本轮开始前的未完成历史合并（索引4097项），本轮没有将这些历史文件混入提交，也尚未发布PR。", "",
              "## 规则来源与追溯", "",
              "- [WizKids MKUE Rulebook](https://wizkids.com/posters/repository/wizkids/MKUE%20Rulebook%20BOOKLET.pdf)：p.3 初始指挥槽/手牌/高级行动展示区，p.7 移动探索，p.8 招募互动，p.9–10 战斗。",
              "- [WizKids MKUE Walkthrough](https://wizkids.com/posters/repository/wizkids/MKUE_WalkThrough_BOOKLET-WEB.pdf)：p.6 昼夜地形费用。",
              "- " + link("测试 XML", BASE / "editmode-results.xml"),
              "- " + link("汇总及证据 SHA256", BASE / "acceptance_summary.json"),
              "- " + link("素材提示词与来源", ROOT / "vibe_coding/codex/rules_scene_art_prompts_2026-09-09.md"), ""]
    (BASE / "验收总览.md").write_text("\n".join(lines), encoding="utf-8")
    print(json.dumps({k: v for k, v in summary.items() if k not in ("scenes", "sha256")}, ensure_ascii=False))


if __name__ == "__main__":
    main()
