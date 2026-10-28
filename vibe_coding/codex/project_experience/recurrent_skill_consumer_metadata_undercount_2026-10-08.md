# Recurrent PPO 技能消费者漏计

最新64局真实轨迹中，原计数器仅识别 skill/skillSideways/skillFork/skillBullseye，漏掉魔咒、两极分化在 play/unit 实际付款时的使用，以及 puppetKeep/puppetUse。原55技能计数保留；本次发现10个真实使用标记变化、涉及3项技能，不代表10种技能或模型提升。证据保存至 AutomationOutputs/BattleRl/20261008_skill_coverage_marker_undercount.json，含原文件SHA、动作及费用/牌区前后状态。

修复只补正式消费者：付款必须原 pending 绑定技能、提交后清空且使用标记新增；傀儡须实际技能标记新增。领导力动作 Card 是单位，须按正式固定技能ID识别，不能拿单位ID记技能。不把选择付款或保留同一标记计为发动。五项受影响检查通过；未改变规则、输入、奖励或优化器。

旧源码绑定不能绕过：旧检查点按新SHA加载应拒绝。显式迁移仅 task_sampler_sha256，72参数/Adam数组及全部RNG/历史/旧计数保持完全相等；原检查点及失败保留，新计数语义从未来912局起生效，旧64局补充证据独立保存，不回填历史。Unity关闭、6400拒绝连接，两次Console零错误仍待补，不能用Python通过替代。
