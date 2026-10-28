# PPO正式合法列推断提速

## 必要修改

当前92664动作的单状态决策先计算整张64×92664权重输出，再屏蔽绝大部分动作，阻碍扩大全原要素训练量。只修改 `tools/BattleRl/train_ppo.py`：新增 `Policy.action_forward(features, legal)`，读取正式环境返回的合法列，计算对应参数分数，最后还原完整概率向量。flat、按类型平衡、类型头/条件参数三种分布均保持原定义；隐层与critic相同。PPO损失、密集梯度、奖励、正式掩码和原规则不变。

rollout和evaluate改用该入口，实际采样的oldlog及价值仍由相同分布产生；批量PPO更新继续原 `forward/loss_grad`。没有重新设计游戏规则或教师代打，也没有下载Torch/消耗VPN传输。manifest显式记录 `inference_execution`，新实现只在后续新进程使用，活v50已导入旧代码不会热切换。

## 等价性与实测

`AutomationOutputs/BattleRl/20261004_v51_legal_inference_equivalence.json`：三种分布的缺失类型、单动作、全部动作、乱序合法列，以及原seed79595的实际各阶段状态，共53组概率/价值/greedy一致检查；41实际操作前后82全状态一致。最大绝对差1.11e-16；非法列概率始终0。保存权重哈希与实际前缀来源保留。单合法动作推断98倍仅属这一状态，不是整体训练速度。

`AutomationOutputs/BattleRl/20261004_v51_original_opening_inference_benchmark.json`实际内容为三个明确标记configured_midgame_inventory的真实可见初始状态（文件名不代表原开局覆盖），合法动作30/13/27，单次推断约0.092～0.098秒→0.0011～0.0022秒，约44/50/83倍。额外正式开启 `BATTLE_RL_ORIGINAL_OPENINGS`，原Arythea等级一seed75236，21合法动作，0.124→0.00221秒约56倍，证据 `20261004_v51_level_one_inference_benchmark.json`。二者均不包含正式规则许可查询、reset或密集PPO更新，不能外推全流程速度或胜率。

Python语法编译通过。flat/balanced/factor在当前熵系数0.05的有限差分梯度通过，原梯度未改。Unity Console两次零错误见 `tmp/rl_legal_inference_final_console_20261004.json`；没有Unity代码/场景修改，本次不重复刚通过的45步防护场景。

## 目标现状

活v50 PID240872/启动15:59:07继续运行，没有重复训练或替换其冻结规则。最新13312/16384步、216episodes；新轨迹10546通用检查零错误、2缺口；12288保存模型七英雄评估0/7完整胜利。下一训练轮才能实测本优化的整体吞吐，仍须改善策略战斗规划、全原要素实际消费者、未见构筑及保存模型Unity回放；本次提速不构成可用模型验收。
