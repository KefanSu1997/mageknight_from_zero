# 独立任务因素必须进入正式初始化，完整任务目标不能由一次战斗胜利代替

新增 `JourneyTraining.Tasks.cs`：明确原英雄、原开局/声明中期库存、库存随机种子、洗牌/魔力/供应/升级/效果/召唤随机流及原敌群序列。不改原效果/费用/合法动作；使用原Hero.CreatePlayer、正式PrepareRound/BeginTurn及原部队/本人技能取得，保持既有指挥槽检查。当前仍按库存种子生成中期库存，没有指定完整卡牌家族/额外牌清单；敌群支持每组1-3个不同原定义、最多16组，重复物理token数量/地点世界合同没有认证。不支持的字段（如直接指定Night）必须拒绝，不能静默退回Day。

旧legacy入口保持全部原状态、reward/done/完整mask/原5710观察坐标；新增3个公开task存在/总组数/当前进度，legacy为0，新featureSchema明确变更，不允许错误旧权重默认为兼容。未输入未来敌人或隐藏牌序。六项实际规则检查证明：换敌群不改库存/随机流，换库存不改敌群，换英雄不改额外牌/敌群，换洗牌只改顺序；旧12步前缀保持；正式nextTurn换组保留牌实体集合、伤牌、名望和同部队状态。

第一次连续组测试把原Arythea轨迹误标Wolfhawk，原专属牌动作正确被合法mask拒绝。没有改原牌或放宽mask，按实际轨迹读取英雄后通过；原首次诊断留本记录。Rules.close关闭已退出子进程的管道，消除重复测试的ResourceWarning，不终止其他程序。

独立任务默认可解目标改为 `complete_declared_enemy_sequence`：不仅要求正式整组End上升沿，还要求完整声明序列且正式EndTurn成功后task_sequence_complete。第一场已赢但没有回合收尾的12步截断见证拒绝；13步完整单组见证通过。多组仅完成第一组不能准入整序列池。原legacy至少一组目标保留为历史/校准，不冒充全任务完成。缓存同时核对原source task/goal/初始hash/动作hash/最终成功条件，而非只看certificate自报status。

实际生成并重放两个独立配置（同英雄/库存、不同原016/032敌人）的完整单组见证；Recurrent PPO用新5713观察和新冻结DLL独立采样两个episode31步、规则检查均0异常、1声明任务完成、一次PPO更新/KL3.824e-7。没有执行见证代打，也不是泛化成绩。产物 `AutomationOutputs/BattleRl/20261007_independent_sequence_gate` 和 `20261007_recurrent_independent_sequence_integration`。

构建0警告0错误，Unity Console两读0错误，无原Unity场景修改。原库基本要素/消费者覆盖、覆盖驱动构筑模板、可解搜索/不可解证明、正式Night起点和跨昼夜记忆实证、独立评估、保存模型Unity操作仍未完成。
