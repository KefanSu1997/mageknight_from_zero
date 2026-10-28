# 原暴徒主动能力和承伤费用

上轮完成昏迷后原部队/技能两阶段验证，有实际进展。本轮推进尚未接入原部队army_2_011，不重跑既有156个部队场景。

## 原始依据与明确边界

已观察 `Assets/GameData/cards/army_2_011.png`：I级，招募费用5，护甲5，村庄/城堡招募；格挡3；攻击3且声誉-1；影响力4且声誉-1；脚注要求分配伤害前付2影响力。旧JSON招募地点误写城市，漏脚注和声誉反转图标，本轮修正并加明确字段。

官方扩展规则（搜索定位）：https://www.mageknight.net/wp-content/uploads/Mage-Knight-Board-Game-Expansion-The-Lost-Legion-Rules-2013.pdf 的 Recruiting Thugs and Heroes，招募暴徒会反转整次交涉的声誉修正，而不是只把这张牌费用取反。现有RecruitmentService尚无整次交涉状态。已给暴徒标明ReverseRecruitReputation并在费用入口明确拒绝错误普通费用；这不是特殊招募完成，必须下一批实现并验收整次交涉。不得将暴徒标为完整通过。

## 修改

- 正式UnitAbility保留声誉变化，解析明确后缀；成功发动后扣声誉，重复/非法发动不扣，最低-7。其余原部队默认变化0。
- UnitCard保留承伤影响力费用。UnitDamageResolver只在正伤害实际分配到该部队时支付；完全格挡、被前支部队吸收完毕不收费。
- BattleResolver先预检整条实际承伤计划所需费用，按当前格挡、迅捷、残暴、元素、抗性与护甲判断哪些目标真的接到伤害；费用不足在任何伤牌、收费、格挡奖励前拒绝，避免两支暴徒只够第一支时半途改状态。正式Assign仍检查并扣真实InfluencePool。
- 复用部队场景，增加定向caseIds筛选、菜单Run Focused Units，更新原素材引用而不删旧场景。生成器保留旧156例及ID，追加156..163共8例；文本明确暴徒特殊招募未完成。定向配置只留每例最终截图。

## 后台

第一次CLI选择写错旧测试namespace，入口在执行前拒绝，没有产生测试结果。改为MageKnight.Tests.OriginalUnitDamageTests后，`AutomationOutputs/RulesState/20260917_072057_e5a55bd7`：29/29，新暴徒10个参数/边界例，以及共享承伤修改影响的19例回归。覆盖全部主动能力、声誉下限、重复拒绝、费用不足/刚好、完全格挡、两暴徒整笔费用、前部队已吸收无需收费。

后台通过后才启动8个新增部队场景。Logic编译07:22:02、Scripts07:22:04，Console两次0。

## 场景完成

`AutomationOutputs/OriginalUnits/20260917_072245_da27af84`：8例，45实际点击步骤+初始视图，共46步骤success；373断言，9张本地截图。用 `tools/verify_unit_scenes.py --output AutomationOutputs/OriginalUnits/20260917_072245_da27af84 --config AutomationConfigs/AllOriginalCards/units_focused.json` 校验独立预期、原图与所有原文、UI点击命中、套件hash和截图，summary.json=passed。MCP的venv没有Pillow，改用Codex已捆绑Python完成校验，没有下载依赖。

已观察020（暴徒影响力4、声誉-1之后，真实承伤付2、部队1伤/英雄1伤）和040（两支暴徒总费用不足，所有伤牌及影响力保持原值）。原卡与脚注完整可读，无截断。运行后Console连续两次0。修改文件diff --check无空白错误，仅换行提示。

8个已过用例退出units_focused日常队列，全部164例仍在unit_cases回归库；本批没有运行旧156例场景。后台保留19例必要承伤回归。卡牌大索引不混入部队子场景统计，整卡目标仍active。截图、日志、报告仅本地，没有推送或网络上传。

下一项：按官方扩展规则实现暴徒招募的整次交涉声誉反转；需核对声誉轨实际修正、一次交涉的冻结/扣款、多个招募共享修正、交涉内声誉变化和无交涉免费获得的区别。当前费用入口明确拒绝，不能把拒绝本身当完成。
