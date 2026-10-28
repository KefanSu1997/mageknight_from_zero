# 类型与条件探索熵的数量偏置

分层动作分布P(kind)P(tuple|kind)的联合熵梯度，会奖励参数数量更多的类型；承伤排序多、提交阶段单参数时，纯熵项就可能推动类型头偏向反复规划。改为类型熵加可用类型条件熵均值，不改合法动作、规则或PPO似然。数值梯度与2/32规划参数的纯熵负对照验证，提交类型概率不再因参数数量下降。证据 `AutomationOutputs/BattleRl/20261004_hierarchical_entropy_check.json`。实际保存策略循环记录 `AutomationOutputs/BattleRl/20261004_v40_4096_greedy_prefix_diagnosis.json`；这是一个可证偏置，不是唯一原因或胜率保证。活旧轮不会加载新源码，必须保留版本归属，等进程自然结束再决定新训练。
