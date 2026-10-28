# 未消费战斗资源、原盾真实出牌与可见操作名称

- 以原规则书第9页不许储存攻击/格挡为依据，EndBattle必须清理全部普通及附身攻击、普通及分次格挡，不能仅清理特殊效果来源。复用CombatPowerPool既有消费入口，同时清除分次格挡目标记录；回合持续效果不在此清理。
- 旧测试若期望普通攻击跨场残留，不能把旧预期当规则。先写下一场实际Attack/Block消费的失败证据，再修复；同时验证新生成资源仍能正常消费、同一分次格挡仍拒绝重复目标。
- 原亡君之盾已有NecroShieldEffect并不代表能从真实手牌打出。PlayArtifact需要原ID映射，基础入已打出区，焚毁进永久移除区；非法选项不得先移动实例。后台与场景都验证真实来源及回合末去向。
- 当场景混合Block、EndBattle、BeginBattle、EndTurn时，显示层不能把所有非Block步骤都叫攻击；应显示准确名称，并呈现卡区状态。JSON通过后仍需要看截图。
- 实际编译进RulesCli的旧测试源文件必须加入报告指纹。仅为路径引用Unity的测试可用工作目录读取原JSON；附身Unity分支仍读原SO，CLI分支读取同源原JSON，不引入Unity库或模拟规则。
- 尚未解决的范围明确保留：EndBattle修复不证明TurnMachine等上层必然调用收尾，也不证明阶段资源期限正确。后续审计实际调用链和格挡触发后续攻击的例外。

过程和证据见 `vibe_coding/codex/worklog_2026-09-17_combat_end_resources.md`。
