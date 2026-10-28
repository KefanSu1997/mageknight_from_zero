# 原部队主要模式：8项待映射定向收敛

仅文档核对，未执行 Unity、RulesCli、网络、生产或 fixture 修改。更新 `unit_primary_evidence_ledger_2026-09-29.json`，保留既有失败和有效历史索引。

## 方法与边界

读取 OriginalUnits 六份 request 的原图 ID 列（非大报告扫描）：20260911 的四份旧批次只有 army_003、army_2_001/005/006/007/008/012/014；20260917_072245 只有暴徒，074532 有暴徒/农民/枪手。不存在斥候、巫师、勇者或魔宠，不能拿旧有限单位总数套到这些单位。

定向查看今日相关 PVE 报告的实际能力按钮、前后资源；对应后台只从已知 XML 读取确切方法 result，未执行新测试。索引没找到的项目不轻率标通过。

## 收敛结果

1. **魔宠基础移动3已有真实场景。** 原林地 `20260929_122149_cee7cf11/report.json`：90步红调谐同魔宠使移动2→5，95/97步各付2真实走格；114步蓝调谐同魔宠使移动3→6，119步丘陵实际付3。来源是已支付招募/维持费的合法有色魔宠，不是未调谐起点。原 `FamiliarActions.AbilityValue` 只在调谐色匹配能力色时加印刷奖励。故旧“非调谐基础值”措辞应理解为**有色标但与当前能力颜色不对应**。
   - 该合并模式改为 partial_scope_passed，基础移动3退出待测。异色攻击3、格挡4、影响3、治疗2没有在所查场景发现。前三个战斗/治疗消费者的后台确切参数分别为 `AttackUsesPaidMarkerAndActualEnemyConsumerWithoutAnotherManaCost(White,3)`、`BlockMarkerFeedsFormalBlockAndPreventsActualWounds(Red,4)`、`HealingPaysForRealWoundCardsAndDoesNotHealTheUnitByReadying(Blue,2)`，位于 `20260923_025452_e400c660/results.xml`，均 Passed。非白影响3未找到确切后台参数，不拿白增强影响5冒充。
2. **四勇者额外影响2归为一个共用场景需求。** loader 四 ID 共用 `AssaultActivationInfluenceCost=2`，`CardUnitActions.ActivationInfluenceCost` 根据实际 Keep/MageTower/City 或进攻玩家身份判断。不同颜色的专属魔力能力已分别有证据。当前大师蓝、红绿、巫师白、村庄勇者四报告所有勇者战斗激活均 influence 0→0，属于普通演练敌群，不是突袭付款；原怪物带城防也不算。无须四颜色重测，只需一个实际突袭中完整2影响付费及后续能力消费者。
   - `20260923_021816_212ab687/results.xml` 中 `AssaultManaAndInfluenceAreBothRequiredBeforeEitherIsSpent(True/False)`、`ExistingTunnelPlayerAttackIdentityAlsoRequiresInfluence`、`InfluenceAbilityCannotPayItsOwnActivationFeeRetroactively` 均 Passed。共享收费对应一个 `heroes_assault_influence_fee` gap；四色记录的后台与场景严格分开。
3. **巫师免费远程3未找到场景。** `20260929_161107_a5453657` 的50/80步实际使用能力1/2且支付白/绿，只证明附加去除的远程3，不能冒充免费能力0。免费能力0的后台确切 `OriginalSorcererModesPayTheirOwnFeesAndFeedRealRangedAttack(0,"monster_032",2)` 在021816 XML Passed。标 scene_needed。
4. **斥候攻城1、移动2未找到场景。** 现 outside_scout 路线只激活能力1私看，随后斥候保持耗竭；原牌完成移动与击杀，没有调用斥候0攻城/2移动。后台 `PrintedSiegeAddsOneToActualOriginalRangedAttack` 位于 `20260923_030209_f193d1c4/results.xml` Passed（原整批31/40，不误写全绿）；`OriginalMovementEntersSharedPoolAndMovesSamePlayer("army_2_009",2,3)` 位于 `20260923_100318_20787bba/results.xml` Passed。两项标 scene_needed，原两格探索仍另列真正缺口。

## 账本口径

30 ID、98模式记录保留：84有限场景、7 scene_needed（其中4记录同一个勇者共用费）、5原明确未场景（四勇者共用精英招募翻倍+斥候探索）、1部分场景、1仅后台。原8项 mapping_needed 已全部完成此轮映射判定，**不是8项都通过**。后续集中连续路线只补真实剩余消费者，不能因此重跑旧已验批次。
