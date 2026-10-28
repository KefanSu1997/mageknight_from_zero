# Critic explained variance

按用户要求加入value_exp_var_before/after=1-Var(GAE_return-V)/Var(GAE_return)。对整个rollout所有时间步合并计算，不先对episode指标平均；targets是实际critic使用的未标准化GAE lambda-return，前后使用同一冻结目标。target variance<=1e-12时JSON null，不强写0或1；记录target variance、samples、MSE、更新后bias，并保存value_update_NNNN.npz中的targets/before/after及episode长度用于复核。

更新前使用采样时的真实value；更新后复用每epoch更新后KL所需的完整GRU序列前向和合法动作头计算，同样前一动作/raw reward历史，不在回合/战斗切换清空。只记录最终实际epoch值，无额外policy前向；不改变loss/gradient/奖励。网页增加前后EV、MSE、目标方差；尚无样本或零方差明确显示缺失。

针对性2测试通过：完美1、常数预测0、反向-3、整体偏移EV1但MSE16、零方差null与NaN拒绝；共享头/实体GRU整序列与逐步value一致、KL完全相同且全部参数未改变。旧8环境轮次仍活跃，不热换；旧轨迹未保存对应每批旧预测，不伪造历史EV。新32环境/128batch队列会加载新诊断。无Unity C#变化；此前本地MCP连接拒绝仍未解除，不将旧Console双零当新检查。
