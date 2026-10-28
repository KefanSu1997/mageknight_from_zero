# 原卡攻击贡献来源（2026-09-29，后台子任务）

## 目的与实现
百步穿杨需要选择实际原牌提供的攻击；不能把所有 SourceUnit 为空的攻击误认为卡牌，也不能扫描新贡献数组推测来源。

- AttackProfile 增加可选尾字段 SourceCard，保留真实 DeedCard 或供应 SpellCard 对象引用；原有元素、攻击类型、击杀奖励等字段不变。
- CardContributionSource 在正式出牌执行作用域内同步 ActionContext 与 CombatPowerPool 来源，使用 Dispose 恢复嵌套前值（含异常路径）。普通行动、手牌法术、神器、横置、供应施法覆盖真实实体；宝盒借用仍归物理宝盒。
- 效能过载的暂停对象捕获宿主来源；后续真实选择 Continue 在同一来源作用域运行，并覆盖最终 CompleteEffect 回调。移除的材料不会被伪称为出牌宿主。
- 纯单位执行必须临时清除来源（由主代理协调 CardUnitActions）；SourceUnit 非空也不贴卡牌来源。后续普通技能或裸池贡献没有遗留卡源。
- SourceCard 使用 object 是因为正式供应 SpellCard 和手牌 DeedCard 为不同数据类型；两者均保留原实例，没有生成替代卡。

## 待验证
已增加 CardContributionSourceTests 共 9 例，覆盖同名不同实例、火法术、神器普通/焚毁、横置、过载延期、失败恢复、供应法术和宝盒。待主代理合批运行 RulesCli；本记录暂不宣称后台或 Unity 场景通过。
