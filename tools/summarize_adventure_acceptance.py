"""独立核对配置、实际UI/状态断言、截图和Console，并生成可查阅的验收台账。"""
import hashlib
import json
import struct
from datetime import datetime
from pathlib import Path

import summarize_rules_acceptance as ledger

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / "AutomationOutputs/OfficialCards/20260910"
NAMES = {"Combat":"林间交锋与遭遇切换", "Exploration":"穿越翡翠山谷",
         "Recruitment":"村庄的盟友", "Journey":"从营地到并肩作战"}
ledger.BASE = BASE
ledger.NAMES = NAMES
read, link = ledger.read, ledger.link


def validate(name):
    config = read(ROOT / f"AutomationConfigs/adventure_{name.lower()}.json")
    report = read(BASE / name / "report.json")
    assert report["status"] == "success", name
    assert report["scenePath"] == config["scenePath"], name
    assert len(report["steps"]) == len(config["steps"]) + 1, name
    count = 0
    for step in report["steps"]:
        assert step["success"], step["label"]
        header = Path(step["screenshotPath"]).read_bytes()[:24]
        assert header[:8] == b"\x89PNG\r\n\x1a\n"
        assert struct.unpack(">II", header[16:24]) == (1920, 1080)
    for expected, actual in zip(config["steps"], report["steps"][1:]):
        assert expected["label"] == actual["label"] and expected["buttonPath"] == actual["buttonPath"]
        assert actual["hitObject"] and actual["runtimeRule"]
        assertions = [(phase, a) for phase in ("before", "after") for a in expected.get(phase, [])]
        assert len(assertions) == len(actual["assertions"])
        for (phase, e), a in zip(assertions, actual["assertions"]):
            state = {s["key"]:s["value"] for s in actual[phase + "State"]}
            assert a["phase"] == phase and a["key"] == e["key"] and a["expected"] == e["expected"]
            assert a["actual"] == state[a["key"]] == e["expected"] and a["passed"], a
            count += 1
    return report, count


def main():
    scenes = {}
    for name in NAMES:
        report, count = validate(name)
        ledger.detail(name, report, count)
        detail = BASE / (name + "_acceptance.md")
        detail.write_text(detail.read_text(encoding="utf-8").replace("`ui:` 字段来自界面实际 TMP 文本。", "`ui:` 字段来自实际界面的 TMP 文本或 Sprite 名称。"), encoding="utf-8")
        scenes[name] = dict(operations=len(report["steps"])-1, screenshots=len(report["steps"]),
                            assertions=count, status=report["status"], finishedAt=report["finishedAt"])
    tests = read(BASE / "editmode-summary.json")
    assert tests["passed"] >= 91 and tests["failed"] == tests["skipped"] == 0
    consoles = [read(BASE / f"console_final_{i}.json") for i in (1,2)]
    assert all(c["result"]["success"] and c["result"]["data"] == [] for c in consoles)
    bad = read(BASE / "ExpectedFailure/report.json")
    assert bad["status"] == "failed"
    assert any(a["expected"] == "999" and a["actual"] == "5" and not a["passed"]
               for step in bad["steps"] for a in step["assertions"])
    summary = dict(generatedAt=datetime.now().astimezone().isoformat(), scenes=scenes, editMode=tests,
                   consoleErrors=[0,0], wrongExpectationRejected=True)
    for key in ("operations","screenshots","assertions"):
        summary[key] = sum(s[key] for s in scenes.values())
    evidence = list(BASE.glob("*/report.json")) + list(BASE.glob("console_final_*.json"))
    evidence += [BASE / "editmode-results.xml"] + list((ROOT / "Assets/Resources/Adventure/Art").glob("*.png"))
    summary["sha256"] = {p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in evidence}
    (BASE / "acceptance_summary.json").write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding="utf-8")
    lines = ["# 原卡接入：四场景验收", "",
             f"Unity 6000.6.0f1；{summary['operations']} 次实际按钮操作、{summary['screenshots']} 张截图、{summary['assertions']} 条独立断言通过。",
             f"全量 EditMode：{tests['passed']} 通过、0 失败、0 跳过。最终 Console 两次 error=0（count=1000，含堆栈）。", "",
             "## 本次纠正", "",
             "移除上一轮七种自制行动卡。五种原卡直接绑定 Assets/GameData/CardsAssets 的 ActionCardSO 和 Assets/GameData/cards 的完整原卡面；基础/强化数值通过既有 ActionSystem 和 CardEffects 执行，没有在场景中另填一套效果数值。", "",
             "| 原卡 | 基础 | 强化 | 所需魔力 |", "| --- | --- | --- | --- |",
             "| 行进 basic_card_000 | 移动2 | 移动4 | 绿 |",
             "| 耐力 basic_card_014 | 移动2 | 移动4 | 蓝 |",
             "| 承诺 basic_card_008 | 影响2 | 影响4 | 白 |",
             "| 决心 basic_card_016 | 攻击或格挡2 | 格挡5 | 蓝 |",
             "| 狂怒 basic_card_021 | 攻击或格挡2 | 攻击4 | 红 |", "",
             "非伤牌横置提供当前阶段的一点通用行动。决心强化不能作为攻击，狂怒强化不能作为格挡；魔力不足拒绝确认，不偷偷执行基础效果。", "",
             "## 场景证据", "", "| 场景 | 操作 / 断言 | 逐步证据 |", "| --- | --- | --- |"]
    for name,title in NAMES.items():
        scene=scenes[name]
        lines.append(f"| {title} | {scene['operations']} / {scene['assertions']} | {link('操作→原卡→数值→截图',BASE/(name+'_acceptance.md'))} |")
    lines += ["", "## 验收方法", "",
              "自动化以 Canvas 射线命中按钮，再发送 Pointer 事件。检查固定预期与点击前后状态、实际原卡 Sprite 名称、实际 TMP 牌效文字、UI 资源数值。汇总再次核对每一条断言、命中对象和1920×1080 PNG，不仅依赖 success 标记。",
              "EditMode 额外核对原 ActionCardSO/原卡面引用、正式名称/ID/耗色/数值、基础二选一、强化阶段限制、横置、优先消费魔力标记、缺魔力资源守恒。故障注入把手牌预期设999而实际5，必须失败，不计入通过数。", "",
              "逐图检查了战斗选牌、伤牌、地图边界、招募、跨回合部队协同。伤牌没有找到现成独立卡图，采用清楚的状态占位，不伪造新卡面；已修复无 Sprite 时的白色方块。", "",
              "## 重跑", "",
              "1. `python tools/build_adventure_acceptance.py`。",
              "2. Unity 菜单 `Tools > Mage Knight > Adventure > Run All EditMode Tests`。",
              "3. `Tools > Mage Knight > Adventure > Run All Four`。",
              "4. `Tools > Mage Knight > Rules > Verify Wrong Expectation Fails`。",
              "5. 按 AGENTS 标准连续读取 Console errors 两次，然后 `python tools/summarize_adventure_acceptance.py`。", "",
              "首次迁移可用 `Use Existing Mage Knight Cards`，只更新五份内置教学冒险的原卡引用、牌序、晶体和目标说明；不改其他冒险或原牌库。", "",
              "## 范围", "",
              "五种原卡各两张组成确定性的10张教学牌组，不冒充英雄的标准16张初始牌库。旧卡库其他牌、技能、图像原样保留；本轮没有将全部复杂抽牌/治疗/弃牌/魔力源、高级牌与法术交互接入新场景。",
              "已有角色/敌人/单位仍是上一轮的场景示例配置；本轮修复行动卡的来源与规则，不宣称已将全部怪物和部队迁移为正式内容。战斗示例仍从格挡开始，尚非包含远程/攻城、七格地图、战役存档的完整游戏。",
              "仅在 Windows 编辑器中验收，没有构建手机或桌面安装包。", "",
              link("复用与扩展说明",ROOT/'vibe_coding/codex/adventure_reuse_guide.md'), "",
              link("工作记录",ROOT/'vibe_coding/codex/worklog_2026-09-10_official_cards.md'), "",
              link("测试XML",BASE/'editmode-results.xml'), "",
              link("机器汇总和SHA256",BASE/'acceptance_summary.json'), "",
              "现有 Git 索引含未完成的历史合并，本轮未暂存或提交该合并。独立差异和文件包另行归档。"]
    (BASE / "验收总览.md").write_text("\n".join(lines)+"\n",encoding="utf-8")
    print(json.dumps({k:v for k,v in summary.items() if k not in ('sha256','scenes')},ensure_ascii=False))


if __name__ == '__main__':
    main()
