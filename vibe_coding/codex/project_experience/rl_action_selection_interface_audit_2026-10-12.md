# 单场RL动作接口复核

用户要求策略直接选择并确认，不把玩家UI预选反悔过程作为RL动作。复核第60次固定验证1000局、66201真实动作；统计保存 H:/MageKnight_RL_Runs/20261012_complete128_tail_safe_100/action_interface_audit_eval60.json。不是所有训练历史，也不是新策略性能对照。

## 实际主要问题

- unitDamage 37286次/403局；其中34180次发生于最终超时局。正式 PveBattleSession.PlanDamage 仅保留目标、攻击编号与部队顺序，不结算伤害。应合并完整承伤分配与确认，保留所有目标/攻击和部队唯一性；不能给一个目标选完就 Advance 整个阶段。
- sharedArm 1886次/22局，sharedCancel 14次/8局；选强化色与取消不是消费。应把强化选色绑定真实后续付款消费者，而不是固定选色或删除原技能。
- defend 932次/28局，clearDefend 101次/21局；敌方Defend能力分配选择，应附着真实攻击目标和来源决策并执行，不混同已实际结算的block/blockSelected。
- payment 64次/20局，cancelPayment 15次/5局；当前源码预检Execute空回调后只记PendingPayment。应把合法付款、物理材料、技能和出牌/部队消费者合成一次动作，不能免费执行消费者。
- 上述七种40298次，占60.9%。这是选择类动作总数，不是全部判定无效；状态比较未发现单步完全相同（除planningRun），循环可经切换不同选择形成，不能只过滤no-op。

## 其他应复合的接口

roundReturn18次/10局、sharedSourceReturn15次/9局、protectionCleanup3次/2局：都是保存清理参数的选择，应归入原正式结束/归还操作。bannerUse总2次，但须按Card items_018强韧预选单独识别，不可把其他真实旌旗发动删掉。bowChoice与killCrystal有源码接口，本次零次，不报告成轨迹已覆盖；应并入攻击消费（保留原弓模式、元素、奖励颜色）。

## 任务边界缺口

rest3520次/562局、fight27次/24局、learn1次。worker_0_episode_11.json在初次战斗结束未胜后nextTurn，进入Outside，随后play/rest；因此只有开局强制战斗，并未让episode限定于一次战斗。严格单场任务应在该场战斗正式结算并完成必需后续义务后记胜负，不继续自由休息再战。nextTurn4373次不能全部叫冗余，含正常胜利收尾，必须按前后状态区分。

source/extraSource会占用真实骰并产魔力，play/unit/attack/blockSelected/heal/转换等有真实规则效果，不因多步或负reward而删除。advance还含合法放弃剩余远程/格挡/近战机会，不能全部自动前进。repeat/共享后续解析可能涉及新信息，仍保留有意义决策；纯确认可自动执行，但不得越过随机揭示替策略选择。

本次只审查与设计范围，未修改运行中的规则DLL、合法mask、奖励、动作目录或checkpoint。后续动作原子化需要新显式动作合同、训练/评估相同入口、正式操作序列逐步留证、前后状态/费用/消费者等值门禁，不能当旧权重同接口直接续训。
