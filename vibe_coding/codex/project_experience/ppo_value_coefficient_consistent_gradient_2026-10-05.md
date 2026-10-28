# PPO价值损失系数的独立实验

v75学习率实验8192步204局1487.05秒，固定仍1/7；Braevalar224次规划，Tovak避战，不能记为改善。v74连续32768也1/7。

为单独检验价值拟合权重，train_ppo新增--value-coefficient，默认0.5保持旧损失；价值MSE项为c*MSE，对应dv=2*c*(value-return)/batch。系数必须有限非负。训练Policy与启动数值梯度检查使用同值，manifest与metrics记实值。动作分布、原规则、掩码、奖励与特征schema不变。旧权重可继续使用，旧Adam未保存因此不声称恢复。

默认0.5在flat、balanced、hierarchical三种分布下损失、梯度、一次Adam权重/历史数组与原源码完全相等。0.1和0的dense/legal梯度一致，三分布有限差分最大误差1.56e-11，零系数价值头梯度为零；负值/NaN/Inf拒绝。首次检查误传list给NumPy mask API，修正检查输入，未改实现。Python编译与实际Unity Console两次零错误。

证据AutomationOutputs/BattleRl/20261005_v76_value_coefficient_checks.json，tmp/rl_v76_value_coefficient_console_20261005.json。实验降低到0.1；价值头范数大于策略头不证明共享梯度由价值主导，更不证明改变系数能提升胜率。必须用真实保存权重评估及失败操作链判定，不能改写原失败或以基础设施通过替代原消费者/Unity验收。
