# 未遇敌只在后续组时追加合法直接开局

实核无活RL后审查当前65/69敌实际遇敌缺口：034/038/040/052在原220池各出现17/16/15/11任务，但首组出现均0。纸面目录覆盖依赖先赢前战，不能说模型已实际见过。追加四独立train组合直接遇敌（Tovak034/Arythea038/Goldyx040/Wolfhawk052），复用原合法构筑/技能/单位/随机流，原长任务完全保留，不用重建连续战状态冒充后续战斗。

四真实reset合法但未知，不称可解/不可解，不把准入计训练覆盖。220→224池保24证书/200未知，全旧定义不变，72参数/Adam/RNG/历史保持；从720实验检查点追加有界128局全局混合，不只训练四新项。PID146616创建03:23:19/session6482，命令8updates×16episodes×2epochs；manifest已写/轨迹持续出现/无failure，不因观察等待重启。四项是否真正被PPO采样与发动仍须后续实际记录，不保证128局全部见到。

无源码/规则/掩码/场景改变，不改原胜利目标，不把局部新根当原长序列验收。验证/test split逐新组合核对train，未搬保留任务。此前冻结3/99未改善继续如实保留，未晋升模型。记录AutomationOutputs/BattleRl/20261008_four_missing_enemy_direct_drafts.json、global_mixed_pool_224.json与pool224_checkpoint_migration/migration.json及活动monitor。全消费者/Unity与旧Console门禁仍缺。
