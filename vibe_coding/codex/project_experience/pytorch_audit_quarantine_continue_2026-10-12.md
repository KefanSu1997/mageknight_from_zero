# 审计失败隔离，训练继续

用户明确要求记录异常、丢弃对应数据、继续训练，后续再修规则。此授权替代此前“审计异常立即结束全训练”的策略。

BattleEnv 仍原样运行审计，先持久化完整 certificate/seed/action/前后状态/report。异常局以 audit_valid=false、is_success=false 返回终止，加入共享任务隔离集合；不提供训练 episode 回报统计，不加入课程胜负计数。多任务训练采样排除该 task_id，固定评估每次分配单任务，因此不剔除评估分母。隔离集合加入 continuation 环境快照。

EvidenceCallback 记录 audit_quarantine.jsonl。保守丢弃包含异常的整个 4096 transitions rollout；全部数组仍归档，train hook 在上游 PPO train 前返回，不更新 optimizer、不增加 completed_updates，rejected_rollouts.jsonl 单独记录。随后重新采样，循环补足有效更新数。behavior/rollout 文件名含真实采样步数以防同一更新编号重采时覆盖。普通合法失败仍参与 PPO。内存、磁盘、绑定、非法动作等基础设施/代码异常不吞掉。

限制：跨 batch 的异常局此前已用于优化的前缀无法追溯撤销；本实现仅保证当前含异常批不更新和该任务未来隔离，不能宣称“整局从未参与训练”。历史实际结果与失败证据均不回写删除。

验证：真实 worker_9_episode_15 的完整轨迹经末步重建，原审计错误精确保留、完整 trace 相等、终止不抛异常且成功标记为 false；拒绝批测试证明上游 train 未调用、更新计数保持5、拒绝4096步。2/2通过。规则疑点本身仍未解决，不得标成规则通过。

恢复输出 H:/MageKnight_RL_Runs/20261012_reward_v2_scaled001_quarantine_100，2026-10-12 14:42:19 launcher215748。从原 scaled001 的5更新checkpoint/Adam/RNG/LSTM续95；显式 audit-quarantine-migration 绑定原 manifest SHA b9c64457d804a09375dbe9391b9b3c7426a88829b2432cc0616470e8b4b511a0，只允许 trainer/env 隔离行为迁移，其余源/规则/数据/奖励绑定必须不变。实际新更新需另核。
