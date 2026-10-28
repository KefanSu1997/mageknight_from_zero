# Transformer结构选择必须显式，默认actor与冻结读取继续可恢复

新增 `recurrent_architecture.py` 工厂，仅显式entity_transformer选择新EntityTransformer32_heads4+三层MLP+GRU，普通默认actor仍原RecurrentPolicy。训练入口增加 `--entity-transformer`，保存架构必须与flag一致，否则拒绝并要求实际完整checkpoint迁移。新架构签名绑定注意力、实体GRU、公开token、工厂四源码SHA和原ID有序词表SHA；冻结reader根据保存flag选择同一工厂并重新检查签名，不能把新37参数族检查点直接喂给旧24族actor或静默忽略新增参数。

上轮实际111数组actor检查点仅加工厂签名，参数/Adam/RNG/所有实际历史全等，零更新。新冻结reader真实加载新actor、旧reader路径加载半步长原actor均通过，原根完整legal概率/价值/GRU差0；故意错误实体源码SHA拒绝。保存 `AutomationOutputs/BattleRl/20261008_entity_actor_factory_binding/binding.json`，不把此动作算作PPO训练或覆盖。Python编译检查通过；Unity Console仍pending。

从同一半步长1120/63153/Adam337实际父状态，保持238混合池、任务/流、LR0.0001、奖励/课程和优化器历史，对普通actor与零列暖接实体actor各启动有界8新局（2更新×4局×2epoch）。当前只是启动：PID90056/99236创建2026-10-08T14:24:25.883534/14:24:26.197275+08:00，完整命令/输出独立保存。先检查首4真实native链一致，再检查两轮后注意力参数是否真正变化与冻结验证。首次零新增列令注意力梯度为0是预期；必须由真实PPO更新打开，不以fixture非零梯度冒称已学关系。后续不热换活源码/DLL或因观察超时重启。

25+213晋升池另存，配对保持原24+214避免第二变量，不遗忘1116证书。未知探索标签不判无解，获胜必须完成声明全部敌群正式收尾。全原要素、费用消费者、69敌能力、7英雄、Transformer真实对照与Unity实际操作尚未完成。

## 真实PPO首次配对结果追加

2026-10-08，两训练进程均自然退出且stderr空。各8新局543实际操作、12739有限审计0错误、0声明整任务成功（首群结果另报）；全部8 native任务/seed/before/action/after/raw链相同。两轮×2epoch后实际Adam337→341，实体新增编码列范数0.070923486；13组注意力参数全部实际改变且Adam一/二阶非零。它们来自正式rollout序列PPO，不是fixture或手工教师动作，仍不能证明有用实体关系或能力提升。

保存 `AutomationOutputs/BattleRl/20261008_actual_entity_pair_training_summary.json`。同预算冻结首组10401×33开发组成正在进行，PID243848/170784创建14:37:13.860771/14:37:14.172818+08:00；其余10301/10501计划按对运行，不在约2.9GiB空闲内存时额外堆叠进程。30最终test仍未用。未知训练任务0胜不判无解，完整原要素、实际费用消费者与Unity仍未完成。
