# 有预算的获胜见证与终止检查点证明

目标：覆盖任务进入胜利训练池前先证明可解；策略失败、有限搜索失败均不能当作不可解。

`discover_task_witnesses.py` 每条搜索分支经正式 reset_task 和真实动作前缀重放，完整合法掩码检查，不另造游戏模拟器。有界分支、规则调用与时间预算；找到完整序列胜利后独立重放并认证，见证不作为 PPO 教师动作。预算耗尽留 unknown 和证据，禁止判为 unsolvable。

首次排序在所有敌人已败后仍鼓励增加攻击资源，造成搜索浪费。只修搜索排序：已全败时不再计无用攻击/格挡资源，优先正式阶段推进及回合收尾；不修改原规则或成功标准。189 次搜索规则调用找到第一项实际训练草案的完整见证；此前两次失败结果保留。

`terminal_checkpoint_proof.py` 精确重放后只对 native Done、无合法后继且未胜利的检查点证明未来获胜不可达。旧 Shaman 伤牌死路90步前缀重放通过；证明明确 initial_task_unsolvable=False，不从策略走到死路推导原任务不可解。一般初始任务不可解求解器仍未实现。

第一项已认证任务上实际 Recurrent PPO 两个 episode 共145步、零完整胜利，审计通过。这恰说明可解性与当前策略能力必须分开。第二项候选326次规则调用后超时，保持 unknown，不因训练/搜索失败永久删除。

证据：`AutomationOutputs/BattleRl/20261007_discovery_complete_priority/solvable_pool.json`、`20261007_discovery_next_candidate`、`20261007_terminal_shaman_continuation_proof.json`、`20261007_recurrent_discovered_task`。新增发现预算与终止证明各两项检查通过；Unity Console 两读零错误见 `tmp/rl_discovery_final_console_20261007.json`。

仅一项真实覆盖草案自动可解，不代表全部原要素已实际使用；细费用/材料/消费者反馈、更多可解任务、独立未调参测试、跨昼夜证据及真实 Unity 操作验收仍待完成。
