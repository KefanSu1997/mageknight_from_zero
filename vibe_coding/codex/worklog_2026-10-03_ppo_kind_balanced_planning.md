# PPO 操作类型平衡与持续策略诊断

## 原因与修改

真实 PPO seed6689 在伤害阶段连续238次预留，直到256步上限。完整轨迹和SHA保存在上一轮 `planning_loop_8192_seed6689.json`，未改写规则或终止活进程。该状态16个合法预留选项、一个推进选项；相同 logit 时，平铺采样天然把16/17总概率分给预留。

`train_ppo.py` 增加可选 `--balance-action-kinds`：每个合法动作 logit 减去该状态同 Kind 合法动作数量的对数。相同 logit 时，各合法操作类型总概率相等，类型内仍由模型学习；未删除任何合法输入，没有自动代打或强制推进。计数只依赖正式可见合法掩码，是固定 base measure，PPO clipped log-prob 和熵梯度保持原正确计算。

分组整数元数据随 NPZ 权重保存；保存模型的评估、重放直接读取，不能训练时平衡、评估时变回平铺。续训若已有分组元数据会自动按新动作目录的 Kind 重新绑定；已有网络权重仍按语义 Key 对应。优化器仅更新可学习参数，不更新分组元数据。仍未保存 Adam 状态，manifest 如实记录 optimizer_state_restored=false。

`policy_diagnostics.py` 从新完成的真实轨迹统计战斗开始/完整胜利/规划步数/最长连续规划/结束原因。周期审查在既有独立规则报告旁记录 policy_behavior；不额外模拟游戏，不把合法规划循环冒充规则断言失败。新增宝盒及公共头环正常/焚毁首次实际使用轨迹留存，避免覆盖计数已有使用而抽样轨迹错过该操作。这里只称实际使用，不自动推定后续消费者通过。

## 验证

`verify_kind_balanced_planning.py` 从 seed6689 真状态精确重放前18步，旧状态、奖励及合法动作一致；23项定向验证通过。

- 零 logit：advance/unitDamage 各0.5；所有正式合法输入概率>0，非法输入概率0。
- 8192旧权重在该真实状态的 advance 总概率0.00559，应用基准平衡后0.08252；仅概率分布变化，不是已训练策略性能提升。
- 普通/平衡 PPO 数值梯度最大误差分别约1.35e-11/1.56e-11。
- 模型保存重载概率完全一致，Adam更新后分组元数据不变，环境状态/观察/掩码不变。
- 本地证据：`AutomationOutputs/BattleRl/20261003_kind_balanced_planning.json`。
- 新诊断复用原轨迹识别最长238次规划；Python语法检查通过。Unity Console两次零错误：`tmp/rl_kind_balance_console_20261003.json`。

## 当前训练与后续门禁

v25长批 PID211504、2026-10-03 22:31:24 启动，观察到15360步124个episode，仍正常运行。新代码未注入运行中进程；不重启/重复启动。当前训练配置仍使用平铺策略及冻结 v25 规则。16384批真实结束后，才从实际保存权重/下一seed启动 v26 + kind balance 的短批，并审查新真实轨迹和重载评估。

实际使用 ID 统计在14336附近为基本25/28、高级38/44、法术20/24、神器24/25、个人技能47/70、部队29/30；这些只是成功入口操作，费用/模式/消费者/原情境完整覆盖单列。日志中的技能71等是选项组合记录数。头环实际训练使用仍为0。

类型平衡只是解决组合数偏置的增量；不等于完成契约要求的变长实体编码、模式→来源→目标→材料/付款完整条件策略，也不代表所有战斗或构筑泛化。最终 Unity 操作验收仍未通过。

## 实际交接

旧 v25 exec session87532 返回 exit0，CIM 已无该训练进程。最终 metrics：16384步、134episode、2248.993秒（含周期评估）、7.285步/秒；下一种子8345。重载七样本6/7至少一次完整胜利、共九胜。最新七条真实训练轨迹632操作、21次战斗开始、三次完整胜利、无步数超时、最长规划39；样本不同，不能作为受控优化结论。最终实际使用ID：基本25、高级39、法术20、神器24、本人技能47、部队30，仍有缺口。

确认旧进程结束后启动新短批：PID213888、2026-10-03 23:14:32，session7337；`train_ppo.py --steps 1024 --eval-episodes 7 --resume AutomationOutputs/BattleRl/20261003_box_same_face_forced_damage_ppo/policy_ppo.npz --seed-start 8345 --coverage-window 21 --audit-seconds 60 --balance-action-kinds --out AutomationOutputs/BattleRl/20261003_headband_kind_balanced_ppo_smoke`。规则DLL v26。manifest已确认 action_distribution=kind-balanced legal base measure，记录源文件/规则DLL/初始权重SHA。尚在运行，未把启动/概率检查当作训练性能结果。
