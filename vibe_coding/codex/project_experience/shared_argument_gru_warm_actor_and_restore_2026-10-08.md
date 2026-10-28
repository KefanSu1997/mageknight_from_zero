# 共享动作残差需接完整序列与117数组，不能只测头模块

新增SharedArgumentEntityPolicy，把已验证公共catalog参数残差接到实体Transformer+三层编码+GRU，原动作列和类型gate保留；历史仍上一实际action/raw奖励，不读未来或动作preview。descriptor复制且readonly，签名包含源码和实际catalog派生数组SHA；不是印刷效果解析或已理解规则。

两个必要完整actor检查通过：初始零projection与原实体actor整序列概率精确相等；非零query/project、GRU、实体投影、历史embedding在真实PPO损失有限差分且非零；两次序列更新保存后再两次更新与不中断参数/Adam/RNG/step精确相等。默认旧头/直接head-only optimizer仍不被共享分支使用，原头scope临时改权重结束/异常都恢复。

factory/trainer/frozen reader加入显式shared-argument-head选择，不匹配保存flag拒绝，新增源码/feature SHA检查。保存旧factory源证据后，从实际1128/63696/Adam341的恢复检查点做对照111数组与共享117数组迁移，所有原参数/一二阶/随机/历史保持，两个新共享矩阵明确初始化、projection0。六正式fight/四advance/nextTurn完整legal概率/价值/GRU差0，两分支恢复/frozen reader通过。`20261008_shared_argument_actor_warm_binding/migration.json`。

双8局对照只一项head结构差异（两支同当前243池/缺口课程，均显式target调整），保原LR.0001/胜利reward、无村庄势能。PID149696/169512创建2026-10-08T18:07:03.552526/18:07:04.102267+08:00；命令独立保存。启动不是成功，后续需首4native链一致、实际共享参数更新/完整保存与冻结。旧1/99失败未删除、6/99不作正式模型晋升。Unity Console/实际操作及全原要素仍pending。

## 真实共享头PPO首次运行追加

两PID149696/169512已自然退出，stderr空。双8局662步、4整任务、15386有限审计零错误，前4native320步以及全8实际task/action/before/after/raw完全相同；共享query/project确经真实序列PPO变化，最大0.00057756/0.00096141，Adam一阶均非零。累计两支1136/64358/Adam345，222.34/225.71秒。相同采样结果不代表学后分布相等或结构有效，原world目标消费者仍缺，不把arg hash叫规则效果。

保存 `AutomationOutputs/BattleRl/20261008_shared_argument_actual_training_summary.json`。已启动两支10401×33独立development冻结，PID323376/58572创建18:17:28.591711/18:17:28.880194+08:00，完整命令/输出保留，后续种子按对运行。注意PID58572是新创建的评估进程，不与旧已退出同号进程混淆。30最终test与Unity全部门禁继续未完成。
