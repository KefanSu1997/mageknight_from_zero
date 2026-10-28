# 头环决斗实际输入与连续消费者

## 修改

在既有HeadbandBorrowResolution增加typed决斗授权，DuelActions复用目标校验和同敌状态，以Strength表示自有1/头环双用2。头环双用原子结算，不让玩家在两次之间消费或改目标；技能留公共供应，不加入自有、不记个人次数。正式近战按同敌发攻击，实际部队介入历史仍取消全部额外奖励。

UI新增HeadbandDuel实际敌人/攻击槽面板，原怪物标记及技能原文；已击败、取消、已确认格挡攻击不可确认。Session也检查ReservedBlocks，避免UI先确认格挡但正式ResolveDefense未记历史时绕过。后者沿本轮上一批已证实的正式时点处理。

后台20有效（新10+直接受影响自有10），详见worklog_2026-09-29_headband_duel_backend.md；第一次失败为测试决心选攻击误当格挡，保留原失败并精准补测。Unity实际Console编译后两次零错误：tmp/pve_headband_duel_compile.json。

## 场景计划

生成器tools/build_pve_headband_duel_acceptance.py复用原升级seed29，主代理改为五原牌000/014/016/021/items022；首战狂怒红费强化4独立击杀狼骑兵，真实两伤保留。实际升级选嘲讽，未选决斗进入公共供应；次战头环Block2+决心基础2挡哨卫4，同敌近战2+行进横置1击杀甲3，原名望2加奖励2。正式回合保留实际牌区/公共技能。首批运行中，未记通过。HornGold临时seed29结束须严格恢复原字节。

此批不处理未核明的狂乱强化/仪式/伪装翻面/傀儡/混乱特殊借用，亦不宣称头环全模式完成。来源分层见headband_remaining_basic_scope_2026-09-29.md。

Unity后续测试导入实际暴露3个internal SkillsUsedThisTurn不可见错误，首次Console双0早于延迟导入，不作为最终门禁。145303_2eaf173e在执行前播放中止，无有效步骤。仅测试改public SkillActions.IsUsed后受影响新增10例145629_8bc24d48全过；生产无改、旧自有10复用，仍20有效而非累计30。实际重编译后tmp/pve_headband_duel_recompile.json双0，重新启动场景。


15:00增量：头环普通决斗已接实际公共技能与同敌同攻击原子双用，格挡2→同敌近战2→合条件名望2；自有保持1，实际单位介入仍取消奖励，预定格挡攻击拒绝。后台20有效；Unity测试internal读取3错已改public查询，受影响10补过，最终Console双0。五原牌合法Wolfhawk真实升级公共决斗连续场景145739_e062eacb一次59步108断言、6图已看，旧伤2/同玩家/技能归属保留。见worklog_2026-09-29_headband_duel_ui.md与pve_headband_duel_acceptance_index.json。145303因延迟导入编译错在步骤前中止，保留未计通过。HornGold严格恢复seed0，无运行测试/代理，无push，goal active。狂乱强化部分结算风险、仪式/伪装翻面、傀儡/混乱借用仍明确未解决；不重复已通过决斗及原卡。
