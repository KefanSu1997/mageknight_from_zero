# PVE部队：原卡激活与可撤销承伤计划

部队作为独立 `BattleUnitDefinition` 绑定原JSON/ID/原卡面，加载为实际 `UnitState`；纯后台和打包场景共用 `UnitJsonLoader.Parse`。不能把文件已存在、JSON能读或某一技能实现，等同于整支部队可用；未完整支持的原部队仍明确拒绝。

`PveBattleSession.UseUnit` 先做当前战斗阶段校验，再调用正式 `CardUnitActions.Use`，保留既有整笔付款、耗竭及效果实现。普通攻击不能在远程阶段发动。耗竭与受伤是两个独立状态：耗竭但未受伤的部队仍可承伤。

伤害分配用实际单位实例组成 `UnitDamageAllocation`；UI选择仅预留，确认前不伤牌、不付款。替换/撤销前先验证完整新计划，拒绝重复单位、非自有单位、已格挡或被取消的攻击。最终仍由 `TurnMachine.ResolveDefense` / `BattleResolver` 结算抗性、护甲、毒素、麻痹、影响力费用和英雄余量。

UI选中单位再点敌人时：激活阶段须清除单位焦点以恢复攻击/格挡结算，承伤阶段须保留单位焦点以便建立分配；不能统一清空所有选择。

本批5后台通过、两场47次指针操作/129检查通过，7张关键图核对，Console双0。证据见 `worklog_2026-09-18_battle_elements.md`。守护者魔像和烈焰枪手只是当前已验证的两种部队，不代表完整部队/神器/英雄技能整合完成。

编辑器启动前实测旧进程为D3D11 swapchain device reset/removed不可恢复弹窗，MCP超时不是忙碌任务。读取实际弹窗后确认退出，并通过Unity Hub既有项目重开；没有重启电脑或修改驱动。当前Editor日志实际位于项目 `Logs/Editor.log`，系统Editor.log只留下迁移说明，不能拿旧系统日志判定编译状态。
