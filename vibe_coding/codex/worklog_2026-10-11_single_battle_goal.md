# 当前目标改为单场获胜策略和 Demo rollout

用户明确：先训练能打赢一场战斗的模型，再在Demo用该policy rollout测试；连战暂不考虑。此前多战、跨昼夜连续任务是历史范围，当前不得作为此阶段验收门槛。七英雄、原卡与规则测试方向保留，但47任务不冒称全部原要素完整覆盖。

核实旧8环境训练已完成100更新，1946累计局/118492累计步/Adam747，原trainer已退出。取消本任务未启动的连战队列PID176520（核实命令与创建时间），不操作其他程序。旧报告、失败、曲线、checkpoint均保留。

prepare_single_battle_scope.py从旧243池精确筛选EnemyGroups长度1，任务definition、ID、种子、获胜证书和未知准入证据不变；20获胜见证+27未知，覆盖7英雄，196多战任务延期。单场仍可能多回合：保留正式结算与状态，不截断为第一下击杀。原complete_declared_enemy_sequence协议字符串保留，长度1时等于单场正式完成，不改变奖励实现来伪造旧胜利。

检查点显式pool SHA迁移：117数值数组完全相等，Adam747/父RNG保留，旧sampler完整归档，attempts只留现池task ID；历史事件继续标预训练，不算新单场验收。输出AutomationOutputs/BattleRl/20261011_single_battle_scope，migration.json可复核。

启动新配置：32worker、128完整局/batch、100更新、最多4epoch；保留印刷输入、GRU/Transformer、step penalty和critic EV统计，--change-collection显式派生32流。输出AutomationOutputs/BattleRl/20261011_single32_batch128_updates100。初始化不冒称32环境验收或策略已可用。后续固定单场验证与实际Demo rollout是当前交付目标；未知失败不判不可解。
