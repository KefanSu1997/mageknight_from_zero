# Part1 战斗测试场景搭建记录

## 背景
- Part1_CombatTest 需提供与 DeckManaTest 相当的完成度：按钮布局、日志滚动、动态面板以及自动化验证。
- 现有 Part1SceneHarness 未覆盖战斗 UI，Part1TestManager 的 TestCombatSystem 仅输出日志且未与 Monster 数据真正交互。
- 目标：玩家进入场景即可理解战斗双方与结果，并能通过自动化脚本重复回归。

## 实施要点
1. **Harness 动态生成 CombatPanel**：模板化标题、切换按钮、英雄/敌军信息卡与结果栏，确保名称稳定（Btn_NextScenario / Btn_Resolve）供自动化使用。
2. **Part1TestManager 战斗数据模型**：新增 CombatScenario 列表，内含 UnitCard + Monster + 推荐格挡数；统一格式化攻击、能力及元素文案。
3. **BattleResolver 示例执行**：根据 UnitCard.Attacks 构建 AttackAllocation，配合 RecommendedBlock 生成 BlockAllocation，真实调用 Resolve 并展示 BattleResult。
4. **状态同步**：RefreshCombatScenarioDisplay 负责 UI 刷新，UpdateCombatOutcome 根据胜负染色并保持最后结果，切换示例时清空历史状态。
5. **自动化覆盖**：新增 `AutomationConfigs/part1_combat.json` 与菜单入口，使自动化依次执行战斗、切换对阵、再次结算并产出截图/报告。

## 教训
- 仅在 OnClick 中刷新 UI 会遗漏初始化场景，必须在 ConfigureCombatPanel / RefreshCombatScenarioDisplay 中集中更新。
- Monster.Abilities 为 IReadOnlyCollection，需要 ToArray() 再拼接，避免多次枚举导致 GC 或空引用。
- BlockAllocation.Element 仍需显式设置为 Physical，否则 BattleResolver 在阻挡阶段会忽略格挡值。

## 自动化验证
- 配置 `AutomationConfigs/part1_combat.json`，步骤依次点击测试按钮、切换对阵、再次结算，report.json status=success 即完成回归。
- 截图关注 CombatPanel 与日志文本同时更新，确认 UI 名称与路径稳定（Canvas/Part1TestLayout/.../Btn_Resolve）。
