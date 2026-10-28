# 实际承伤提交与训练重复预选代价

真实混合课程失败Tovak前16步严格重放状态/动作/原奖励与GRU选择概率。13/14/15的advance均实际合法，概率约.4737，同前缀正式提交都进入Melee；完整前缀＋替代提交审计通过。循环是对同一攻击的有序承伤预选反复替换（102三部队与2单部队），不是实际部队发动/已承伤，更不是提交入口缺失。JourneyTraining554/560已观察计划存在及顺序/物理槽，不随意宣称观察缺失。

新增planning_revisit_reward.py可选独立训练代价：连续unitDamage内，首次记录原计划与新计划不额外扣分，重新访问公开有序unitDamagePlan才扣指定cost；其他真实动作结束该连续段。默认0保持原奖励，原始环境奖励/actor历史reward始终分存，不改规则、mask、动作或玩家撤销能力。checkpoint保存cost和源SHA，恢复变化显式change-planning-objective；冻结加载/扩池校验源SHA。两项新检查覆盖首次/重复/提交重置与disabled/非法cost；隐式目标变化负例拒绝。

历史真实243个规划动作中241重复，训练-only反事实-.25累计-60.25，不作为教师样本。由真实264局检查点完整恢复的32局实验1500步11完整成功，35290有限审计零错误，CPU133.670秒；所有1500训练reward逐行重算一致，实际只有7重复预选受罚，raw保持。Console两次零错，规则/观察/场景未改。

冻结分层仍5/13/1226步四长循环，固定四validation仍0/4；同seed9501采样校准前6/13后4/13。小样本非因果结论，候选未晋升、源与新权重及全部失败保留。证据AutomationOutputs/BattleRl/20261007_planning_revisit_iteration_summary.json、damage_toggle_actual_commit_diagnostic.json与各actual reward/负例报告。继续保留完整原范围/消费者/Unity/test缺口；不能以代价代码存在冒称循环修复。
