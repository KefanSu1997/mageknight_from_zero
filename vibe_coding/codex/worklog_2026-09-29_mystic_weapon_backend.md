# 宝盒单层原武器与恐惧旗战斗入口

## 修改
- PveBattleSession 复用 PlayArtifact、PlayHorn、PlayShield、BurnFearBanner、PlayResourceArtifact。内部 BattleArtifactId 只认可同玩家、同实际已打出宝盒、尚未使用的揭示结果；按原神器 ID 保留原战斗阶段与材料选择限制。
- ExecuteBattleArtifact 单处分流：普通原卡走原 PlayArtifact，宝盒实体走既有 UseMysticBox；不构造替代神器、不复制效果规则。普通/焚毁移动的是宝盒，实际供应揭示原卡在正式回合末归底。材料与目标仍 finally 恢复。
- 原星辰之弓逐贡献选择、灵魂收割者实际击杀颜色沿用原消费者。

## 定向验证
- 新 PveMysticWeaponEntryTests 7/7：公正剑与星弓真实弃牌/物理来源；盾牌阶段拒绝、假同 ID 实体拒绝与双真实攻击槽；号角焚毁追加3、实际三骰及伤牌和攻城；恐惧旗双实际目标、重复目标失败守恒、输入恢复；收割者实际击杀得晶；黑暗护符显式选蓝标记。
- 每项核对实际供应原卡归同堆底；已用宝盒不再亮可用入口。7/7 报告 AutomationOutputs/RulesState/20260929_130807_e2e1337d。
- 原入口直接回归2/2：PveFearBannerTests.BurnCancelsAllAttacksOfThreeChosenEnemiesAndOnlyBurnsRealBanner、PveWrathHornTests.InvalidBonusForeignInstanceAndBlockPhaseRejectWithoutDrawingDice。报告 AutomationOutputs/RulesState/20260929_130826_8992fb56。
- 首次CLI编译曾因测试误用AttackProfile.Amount失败，改为正式Value后以上全部通过；未修改生产数值/费用。
- 后台通过不代表场景通过；Unity编译与场景由主代理负责。本轮无Unity操作、无全库重跑、无远端传输。
