# 叉形闪电与真实目标贡献后台

## 规则与方案

原图 `Assets/GameData/cards/skill_en_003.png` 末行对应 `hero_skill_pool_001:05`：每回合一次，对至多三名不同敌人各产生冰火远程攻击 1。复用正式技能取得、使用次数、实际 Monster 实例和共享攻击消费者；不合成一笔可任意打给单敌的攻击 3。

`ForkedLightningActions.Use(player, context, skill, targets)` 在所有目标、阶段、归属与次数通过后创建贡献。每笔保留 `RequiredEnemy` 与 `SourceSkill`，纯技能不冒充原牌来源。未参与当前攻击的目标贡献保留；消费按引用，避免两名同数值原敌的 record 相等性误删另一笔。

`CombatMath`、共享贡献池、CardCombatActions、BattleResolver 和 RangedPhaseResolver 使用一致限制。组攻沿用现有整组抗性；目标限定贡献最多覆盖对应敌人的有效护甲，通用贡献填补剩余护甲，低效半点最后一次取整。此为原技能分别作用目标与现有组攻规则的组合实现，本地没有找到叉形闪电组攻的官方专门算例，不能称为已获专门 FAQ 核准。

同时接入决斗真实部队影响登记、实际击杀奖励及傀儡真实击败候选钩子。击败先入实际索引，再登记傀儡，最后处理世界敌标记归还。格挡限定支持真实敌人、攻击槽、部队来源；无效目标前置拒绝。

## 验证

- 主代理统一 CLI 报告 `20260929_064218_44476d64`：`MK.Tests.Battles.PveForkedLightningTests` 13/13 通过；同批决斗 10/10 通过，其他模块的失败单独处理。
- 覆盖实际三目标、同值不同实例重排、组攻容量与真实火球补值、非法目标零消耗、死亡/阶段拒绝、城防、半点取整、正式跨回合清理。
- 主代理精准共享回归 `20260929_064412_67c047c2`：8/8 通过。包括组抗取整、冰火抗性、底层低效格挡、连续近战奖励、星弓倍率、亡君之盾同敌多攻击槽、附身同值实例。
- 一个测试名原为 `ResistedHalvesJoinOnlyAtWholeAttackAndRestrictionsPersistThroughConversion`，实际只断言半点合并，已更名为 `ResistedHalvesJoinOnlyAtWholeAttack`，逻辑未改，不重复运行。不据此宣称转换专项通过。
- 后台通过不等于场景通过。Unity Console 和集中场景验收由主代理继续，此记录不宣称场景完成。

## 修改范围

`AttackProfile`/`BlockAllocation` 尾部可选字段；`CombatPowerPool`；`CombatMath`；`CardCombatActions`；`BattleResolver`；`RangePhaseResolver`；新 `ForkedLightningActions` 和独立测试。无网络传输、推送或 Unity 操作。
