# PyTorch 固定奖励缩放重新训练

用户要求直接重新训练归一化 reward。2026-10-12 14:11:38 启动 `H:/MageKnight_RL_Runs/20261012_reward_v2_scaled001_100`，launcher 218088 / Python 156084，fresh 100 updates、CUDA、32 environments、128 steps/environment、minibatch 128。旧 memory_safe_100 已定向停止，14 更新记录及轨迹保留于原目录，superseded.json 记录交接，不继承旧优化器。

所有 single_battle_v2 奖励统一乘 0.01：胜项 +1、败项 -1，击杀预算 0.1、英雄伤牌成本上限 0.2、步成本上限 0.05。胜利总回报至少 0.75，失败至多 -0.9。固定尺度缩放不是动态均值方差归一化；PPO advantage 标准化维持上游实现。环境返回、历史 reward 输入、评估 reward_config 一致。物理胜利、击杀、伤牌指标不缩放。禁止只缩图冒充训练改变。

已用真实归档 100 局 4865 步（3 胜）核每一步恰为原奖励的 0.01 倍及胜负回报边界；并行评估 1/3 worker 独立 RNG 和记忆对照测试通过，并断言 reward_config 传播。新 manifest 保存 reward_scale，跨尺度 resume 拒绝，页面读取 manifest 标明实际尺度。

诊断 H:/MageKnight_RL_Runs/gradient_diagnosis.json 的四个真实 minibatch 中，三个 critic 梯度范数约 816–863，actor 约 0.11–0.22，全局裁剪系数约 0.0006；这支持尺度失衡排查，不证明它是全部原因，也不能直接把裁剪系数当 Adam 有效步幅比例。旧初始冻结 29/1000 不可冒充新 reward 历史输入下的初始基线。新初始模型和 RNG 保存；固定评估每 20 更新进行，避免训练与评估规则进程同时驻留。

启动及缩放验证只证明集成，不证明胜率改善。须读取实际新更新、EV/MSE/KL 与固定完整验证再判断。
