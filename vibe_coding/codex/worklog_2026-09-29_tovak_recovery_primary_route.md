# 托瓦克无惧痛苦与激励连续路线（本次主要消费者通过）

独立生成器：`tools/build_pve_tovak_recovery_primary_acceptance.py`。默认仅输出计划；`--write` 才写配置，`--from N` 只重放旧前缀不重复其断言/截图，`--output` 支持独立目标。未修改共享 fixture，未运行 Unity。

```powershell
& 'D:/study_and_work/unity_mcp_source_code/unity-mcp/Server/.venv/Scripts/python.exe' -X utf8 tools/build_pve_tovak_recovery_primary_acceptance.py
# 由主代理通过后台门禁后写入并串行运行：
& 'D:/study_and_work/unity_mcp_source_code/unity-mcp/Server/.venv/Scripts/python.exe' -X utf8 tools/build_pve_tovak_recovery_primary_acceptance.py --write
```

复用既有 HornGold（39）种子0和红蓝各一晶，无预配部队。原 Tovak16牌经正式 OriginalHeroes 洗混；合法等级6、名望35、护甲3，两技能低于上限3。原先已通过同种子原16牌场景给出共同洗混位置，但新路线**仍由新增后台实际创建同一原英雄牌库来核对**，不调整共享种子或注入牌序。

第一战原032攻击4实际造成2伤，原狂怒2+行进横置1击杀护甲3。正式结束回合保留两伤；次回合自身无惧痛苦弃第一张真实伤至弃牌堆并抽下一张原狂怒，激励抽2获得蓝标。原耐力强化移动4实际消费蓝标，库存蓝晶1保持；无惧痛苦新抽狂怒和激励新抽凝结各横置移动1，真实支付平原费用2。最终同玩家18张实体（原16+战伤2）、手6/库6/弃6，手伤1/弃伤1，无永久去除。

时机已读源码核对：无惧痛苦战斗外每回合一次，激励每轮一次；激励互锁仅约束其他激励，前用无惧痛苦不会冲突，无需换轮。现有无惧痛苦没有专属伤牌选择UI，正式效果的合法默认是第一张实际伤；不为这次验收添加无必要生产入口。伤牌的引用身份由后台检验；场景用手伤/弃伤、原图及牌区守恒验收。这里使用已有演练敌人/地图，不冒充原七格肆虐路线。

## 精确后台复用与新增门禁

- `JourneyRestSkillTests.FearlessPainDiscardsRatherThanRemovesAndFailedUsePreservesUsage`：默认/实际伤牌弃置而非治疗移除、抽真实牌、次数/战内拒绝。
- `PveRecoveryInspirationSkillsTests.InspirationDrawsActualCardsAndRewardsStrictlyLowestFame`（007,Blue）：Tovak实际抽2、最低名望蓝标及费用消费。
- `PveRecoverySkillJourneyTests.InspirationDrawsSameOriginalInstanceThenPaysItsEnhancedEffect`（007,basic_card_014,Blue,4）：新抽原牌引用及原蓝费移动消费。
- 既有有效报告：`20260923_052148_b1cc084b`、`20260923_052243_35e02af0`（journey_rest索引有效范围）；`20260929_022702_87753db7`、`20260929_023014_2d4a1e11`。不因新增路线全量重跑。

新增独立测试文件：`Assets/Tests/RulesState/PveTovakRecoveryPrimaryJourneyTests.cs`。仅此一个方法是新组合门禁：

`MK.Tests.Battles.PveTovakRecoveryPrimaryJourneyTests.ActualBattleWoundThenOwnFearlessPainAndInspirationPayOriginalMovement`

它从 `OriginalHeroes.Get("Tovak").CreatePlayer(originals,new Random(0))` 真正洗混，经过真实敌人伤害/攻击、正式回合和两技能，检查新抽牌确切来源、两张原狂怒不是同一实例、蓝标优先支付、位置/费用及18实例守恒。不模拟规则、不写入手伤或牌堆顶来凑预期。

当前未运行后台/Unity，不记通过。主代理跑部队场景期间已暂停Assets写入；其完成后授权恢复，已完成测试中默认第一伤选择和零蓝标 GetValueOrDefault 读法的两处对齐，现可串行执行上述唯一新方法。生成器未写共享配置。

## 实际结果（取代上方准备阶段状态）

- 后台 `AutomationOutputs/RulesState/20260929_181842_1d35fa05`：上述新组合 1/1 通过，确由原 Tovak16牌/种子0及正式战斗生成两伤；引用身份守恒断言通过。
- 场景 `AutomationOutputs/PveBattle/20260929_182034_f35c6ed5/report.json`：48操作、125断言通过。主代理目视26/31/33/37/47五张关键图正常。
- 独立读取实际状态：31步手伤2→1、弃伤0→1，抽到原狂怒，技能次数True,False；33步再抽凝结/敏捷并得到蓝标1、技能次数True,True；37步耐力强化移动4，蓝标1→0而库存蓝晶仍1；47步同Tovak两回合18实体、手6/库6/弃6、手伤1/弃伤1。
- 最终 `tmp/pve_primary_delta_final_console.json` 两次实际success且data空，即0错误。
- 证据索引 `pve_tovak_recovery_primary_acceptance_index.json` 保留报告/XML/Console哈希、原路径与关键状态，不重算截图哈希。

元数据纠错：`resources/text_json/basic_card.json` 与 `Assets/GameData/CardsAssets/basic_card_002.asset` 均确认002名为**凝结**。生成器旧标签“魔力结晶”现改为“凝结”；原卡ID和实际动作始终是002横置移动1，原图正常，不改历史report、不重跑动作。仅此标签/文档修改，未改生产。
