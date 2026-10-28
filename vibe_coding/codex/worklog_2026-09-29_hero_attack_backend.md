# 2026-09-29 英雄真实攻击槽后台

本记录仅描述后台；Unity Console与连续场景由主代理另记。范围：元素抗性001:00、身经百战003:01、诅咒003:08、嘲讽004:06。规则来源为既有`Assets/GameData/BattleDemo/original_skills.json`与`CardJsonLoader`正式技能。

## 改动
- 四技能均加入正式`ICardEffectValidator`，失败不耗次数。option统一0/1表示原效果分支，真实目标由`SelectedEnemy`引用和`SelectedEnemyAttackIndex`提供；不把敌人索引编码进option。
- 元素抗性仅分伤阶段，0下一次火/冰英雄伤忽略2，1下一次其它英雄伤忽略1。部队先承伤；仅实际分给英雄的匹配伤害触发；不足额度也整次消费，不泄漏到第二槽。
- 身经百战仅分伤阶段，0物理2、1非物理1；要求实际可攻击槽且类型匹配，将许可绑定敌人引用和槽。另一槽不消费；命中所选英雄承伤后清全部额度。
- 诅咒0所选槽攻击-1、1实际敌甲-1最低1。远程阶段拒绝自身或地点城防敌人；减攻拒绝已经击败、取消、停攻、成功格挡、近战时槽。
- 嘲讽只格挡阶段；0所选槽攻击-1；1该槽攻击+2且同敌甲-2最低1。发动时停攻拒绝，发动后该槽被取消/敌人停攻时减甲不生效。
- 技能攻击调整用`Monster`引用+攻击序号列表，防止同名等值实体及敌人重排串目标。`CardCombatActions.DefenseProfile`统一组合调整，原卡`PendingAttackReductions`与其不同目标拒绝语义不改。
- 嘲讽减甲动态读取真实停攻；`CardCombatActions.Attack`原为防重复承伤临时屏蔽全部敌人，现保存实际停攻集合供减甲判断，避免临时结算实现污染原规则。
- 正式战末`UnitDamageResolver.EndBattle→EndBattleWounds→ClearAttackReductions`清四减伤额度、新攻击槽调整、身经目标；`ClearSkillArmorReduction`清嘲讽引用列表。正式回合末也经既有ClearAttackReductions清许可。

## 后台证据
- 新夹具`Assets/Tests/RulesState/PveHeroAttackSkillsTests.cs`：首次24项23过1失败，`AutomationOutputs/RulesState/20260929_030431_f76491f7`。失败真实抓到清理插入脚本未生效，已保留。
- 修正正式清理后仅补失败方法`BothDefensePermissionsAndSlotModifiersClearAtRealBattleEnd`，`20260929_030544_23c40ef6`，1/1过。有效新增24项。
- 直接受影响共享槽消费者：`PveTargetedDeedEntryTests.QuickReflexesReductionIsConsumedBeforeSwiftBlockCalculation`及`OutmaneuverSplitUsesDistinctActualSlotsAndAwardsAfterRealDefense`，`20260929_030655_84ac4d79`，2/2过。
- 总26项有效后台；覆盖物理/火/冰/冰火减伤分支、原部队先承伤、许可整次消费、同实例双攻击/相等不同实体、实际索引变化、城防拒绝、无效输入不耗次数、嘲讽真实减甲击杀和后续停攻、session入口临时参数恢复与真实伤牌。
- 首次CLI编译缺`using System`和测试错误`CancelAttack`方法名，已改为正式`CancelEnemyAttack`；无生产规则退让。
- 补清理脚本曾将额外行也插入`ResolveNoWoundAttack`，在场景前精确去除；随后2项共享消费者回归通过。最终代码修改完成后已通知主代理进行Unity最新Console检查。

## 边界
后台为真实正式消费的人工数值单位夹具，连续场景仍须原敌/原卡/合法英雄构筑验证；不能以此当场景通过。洞悉猎物、其它技能未在本批接入；不扩大全卡库回归。
