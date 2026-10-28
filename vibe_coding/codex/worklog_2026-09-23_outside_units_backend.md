# 连续旅程自有部队战外入口（后台阶段）

## 问题与方案

30种原部队已可解析/选图，但连续旅程只有战斗控制器的部分战后产魔/恢复；移动、交涉影响力以及下回合战前部队发动没有入口。只补通用 facade，不重写正式能力，不把选择目录当作可玩完成。只读逐部队审计见 `tmp/pve_units_entry_gap_20260923.md`。

新增 `Assets/Logic/Runtime/Battles/PveJourneySession.Units.cs`：

- `OutsideUnitAbilities(UnitState)` 返回当前战外窗口的原能力索引，不承诺费用、目标或就绪验证已经成功。受伤/耗竭的原能力仍可显示，正式确认时拒绝。
- `UseOutsideUnit(unit, abilityIndex, option=0, unitTarget=null, manaColors=null, scoutTarget=null)` 要求实际活跃未结束回合、战斗外且已完成待续重复效果，复用正式 `CardUnitActions.Use` 的费用/耗竭/真实目标/原数值。
- 允许原移动、影响力、移动/影响二选一、固定/自选产魔、治疗、重整、全体抗性、斥候私看。普通移动和斥候移动/私看要求在行动前；治疗及特殊产魔保留正式战后/休息后窗口。
- 重整必须指定真实自有部队；治疗先进入正式治疗池，后续由既有治疗消费入口选择创伤。非重整能力拒绝误带部队目标。没有伪造探险动作，私看复用真实地图/供应 `ScoutTarget` 与 `LastScoutObservation`。

## 必要后台验证

新增同源 Unity/RulesCli 测试 `Assets/Tests/RulesState/PveJourneyOutsideUnitTests.cs`；一次只运行该类：

`dotnet run --project tools/RulesCli -- . MK.Tests.Battles.PveJourneyOutsideUnitTests`

`AutomationOutputs/RulesState/20260923_100318_20787bba`：**17/17通过**，失败0/跳过0。

覆盖护林人森林实际减费移动、农民/斥候移动真实消费；七种原部队在真实村庄交涉中产影响力、声誉变化、购买治疗并消费实际伤牌；火法产红标记+晶体后原火球支付/击杀且库存晶体保留；休息后药草师/绿勇者印刷绿费治疗；药草师重整同一受伤农民但不治疗、真实跨回合保留；斥候私看真实守军不公开揭示；错误费用、外来目标、错误部队目标、行动后移动、战斗中与旧回合上下文拒绝。

## 明确未完成

- 本提交只完成后台 facade；主代理接真实 Unity UI、必要字段观察、集中连续场景和 Console 双0。此处未启动Unity也未取新截图，**不计场景通过**。
- 魔宠实际招募选色/合法地点、斥候原板块探索消费者、供应奇袭队攻击槽UI仍是审计缺口；既有维持、私看原规则不重写。
- 未重跑既有部队19/30有限场景基线，也未提高部队完整场景统计；没有远端传输。
