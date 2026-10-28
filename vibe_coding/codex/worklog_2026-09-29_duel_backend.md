# 决斗后台与真实单位介入历史（2026-09-29）

本批复用 Wolfhawk 原技能004:07，原英文卡 skill_en_015.png 第二行。仅PVE；不宣称PvP或场景通过。

## 实现
- DuelActions将选定Monster引用、格挡攻击槽、普通攻击阶段一次攻击1、实际单位介入历史和单次击杀奖励绑定到同一战斗状态。原技能频率和归属在任何状态修改前校验。
- 贡献代理统一添加RequiredEnemy、RequiredAttackIndex和FromUnitAbility；部队供应借用虽无SourceUnit也保留能力来源。真实格挡/攻击消费处登记作用的敌人，未消费的部队贡献不误算。
- CardUnitActions实际停攻、冻结减甲、奇袭及有实际防御可去除的原附效登记目标；大师和恐惧旌旗同样走实际目标。自然之力增强属于卡效果，基础只给予部队抗性，最终实际单位承伤才影响决斗资格。
- 部队元素/攻城转换登记来源，并仅在后续实际目标攻击中转换生效时影响资格。
- UnitDamageResolver在真实非零伤害通过验证及付费后登记，即使抗性完全吸收且零伤牌。战末清历史不复原技能本回合次数。
- 共享ActionContext/旧效果/UI由主代理集成；CombatPowerPool/CombatMath/CardCombatActions/BattleResolver由贡献代理接消费者与奖励，避免双写。

## 验证状态
新fixture MK.Tests.Battles.PveDuelSkillTests 共10例已写，等待协调CLI。尚未后台通过、未启动Unity、未推送。
覆盖同敌/不同同名敌、原技能合法阶段、实际单位与借供应格挡、之前的未杀远程攻击、抗性吸收承伤、拒绝与零伤害、恐惧旌旗/大师、单位转换和战末清理。只有必要新增和直接受影响旧用例纳入本批。

## Unity测试程序集修正
Unity发现测试直接写内部setter OfferedUnitUses（CLI同程序集未拦截）。已改 ActualOwnedOrOfferedUnitBlockCancelsOnlyAffectedEnemyReward 的 offered=true 分支，通过公开正式 BattleSummonsEffect(false).Execute 建立许可；生产setter不变。本次遵循主代理安排未运行CLI，待Unity重新编译与必要受影响验证。
