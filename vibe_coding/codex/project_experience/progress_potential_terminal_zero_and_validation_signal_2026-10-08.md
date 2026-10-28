# 终止势能归零与固定开发验证正向信号

完整33冻结验证0完成提示多战信用分配薄弱，不能据此判任务无解。新增可选ProgressPotentialReward，以原公开encounterIndex/groupCount及正式End/won计已完成进度，F=.995*Phi(after)-Phi(before)，所有native终止的Phi(after)=0。重复已赢阶段不会正向刷分；默认0完全保持原回报。原奖励/actor历史仍raw，训练额外项独立保存，PPO胜利定义/原规则/掩码不变。

新单测覆盖成功/失败折扣望远镜抵消、阶段重复与disabled/非法scale；128旧真实轨迹总折扣差最大2.25e-14，51早期进度事件，只诊断不作教师。恢复改变须显式change-progress-objective，scale及代码SHA随模型保存、冻结加载/扩池校验，隐式变更负例拒绝。

从真实400局检查点完整Adam/RNG恢复，64新局3601步10完整成功、30未知局、84204有限规则检查0error，CPU321.330秒，保存464累计局21953步Adam189。全部3601实际训练reward逐步重算一致，折扣总差最大3.20e-14。没有价值头重置或修改原物理规则。

相同预分33validation组成/同初始actor RNG10301，冻结前0/33后3/33；组胜11→20。新成功Braevalar061→06251步、Goldyx005→00654步、Tovak013→01459步，当前正式规则全前缀认证；评估0优化器更新、权重SHA不变，48952有限审计0error。每任务仅一次随机尝试，源policy变化会改变后续RNG消耗；额外64局与势能一起变动，不能因果归功于势能或称稳定泛化，所有成功均双战、长六战仍缺。development validation参与迭代，不当untuned最终test；30test仍未用。

源码是Python奖励/训练/加载门禁，Unity原规则/场景未改；Unity当前未运行，Console两零错门禁尚待补，不伪称代码全验收完成。证据AutomationOutputs/BattleRl/20261008_progress_potential_all33_validation/all_attempts_comparison.json与actual_return_review/implicit_change_rejection及真实训练目录，失败/旧新权重保留。完整原范围/费用材料消费者/初始无解solver/保存策略Unity未达标。
