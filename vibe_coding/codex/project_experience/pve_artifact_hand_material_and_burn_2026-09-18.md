# PVE神器：实际手牌、材料、附旗与焚毁

神器使用已有 ItemCardSO、原卡面、正式 `CardPlayActions.PlayArtifact`。战斗层只管理合法输入与阶段，不复制效果数值。`BattleArtifactDefinition` 绑定源ID；`BattleLoadout` 为每一张出战神器构造独立实际 DeedCard。

点材料时保持正在使用的神器为当前卡；另点手牌仅切换材料实例。普通/焚毁切换清空不适用材料，不能把武器自身作为材料。动作后恢复 ActionContext 临时目标与材料，避免把一次选择带到下一次命令。

旗帜普通附着的实际牌既不在手牌，也不进入已打出/弃牌区；焚毁必须留在 RemovedCards。承伤与单位显示护甲都要通过正式旌旗修正计算，不能仅用UnitCard印刷护甲。普通武器材料则要真实进入DiscardPile，不能只减计数。

CLI同源测试编译在同一程序集，可能意外允许测试访问逻辑内部字段，而Unity的独立测试程序集不允许。本批 `HonorBannersThisTurn` 引起一次Unity CS1061；改为验证正式公开 `BannerActions.HonorBonus` 的结果，只补2个受影响后台例并完成Unity Console双0。不能用CLI通过替代Unity编译检查。

本批证据：5个独立后台、4场106点击/270检查、8关键图与Console双0，详见 `worklog_2026-09-18_battle_elements.md`。仅荣誉旌旗与公正之剑普通/焚毁的PVE整合通过，不推广为25种神器全部接入。
