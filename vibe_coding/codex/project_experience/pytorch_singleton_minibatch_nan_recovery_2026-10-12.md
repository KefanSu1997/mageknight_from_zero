# 完整局变长批次的单样本尾批 NaN

2026-10-12：complete128_horizon128_100 第69批8193个有效动作，8193 % 128 = 1。SB3-Contrib RecurrentPPO 对最后一个真实样本 advantages[mask].std() 使用无偏标准差，产生 NaN，随后 Categorical logits 全NaN退出。最后有效更新68。原stderr、69批完整归档与轨迹保留；不是规则错误，不隔离这128个合法episode。

compact_recurrent_buffer.get 保持上游准备、随机循环切分、序列边界和padding，唯一调整：最后剩1个真实样本时并入前批，最后129个。其余批128；不删样本、不补假样本、不修改奖励与PPO损失。7种长度127/128/129/256/257/8192/8193验证新旧索引顺序完全一致，普通余数分批不变。

原失败8193动作buffer与68检查点在CUDA执行上游优化，参数及Adam状态全有限（按原targetKL在epoch2提前停止，不宣称完成4轮）；singleton_tail_gate.json保存结果。32环境128有效局+1异常隔离补采/完整回报/参数更新回归1/1通过。

train_sb3_episodes_tail_safe.py绑定父manifest SHA 224730c9d5d7002b221745f2baead8108f9b94a92b093cd2e7bdb1835380681d，仅允许buffer源码迁移及新入口，保持旧训练器源码和其他绑定。恢复68权重Adam/RNG，从新完整局继续32次至100。失败69采样保留但不当有效优化。额外规则审计task fde0b937e76c0d0825598e7fa3b5d4f73e70941f79e76afc3166e6ca7857930e 的45步已单局隔离并补采，其规则定性仍待查，不与优化NaN混淆。
