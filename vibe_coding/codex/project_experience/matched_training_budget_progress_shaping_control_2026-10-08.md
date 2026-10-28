# 等训练量势能对照须保相同起点与真实首批

核实无活RL后，从同一真实400局checkpoint/full Adam/RNG/global220池追加无势能64局，8updates×8episodes×2epochs、LR.0002/terminal_sequence与势能支一致，唯一训练配置差异scale0/20（及开启源SHA）。第一批8局491真实before/action/after/rawreward完全相等；不声称后续on-policy轨迹仍相同。参数/RNG非重建，test/validation没有回填训练。

无势能支64局3414步14完整成功，CPU297.917秒，保存464局21766步Adam189。冻结同33development validation × actor seeds10301/10401/10501，全部0/99；势能支此前4/99。此匹配实验支持势能可能贡献于有限成功，但只有一个训练随机起点且四次成功集中少数双战任务，不能说稳定通用提升或有统计确定性。训练中14vs10不是冻结策略能力排名。

首批匹配、实际训练/冻结源SHA与全部失败保存AutomationOutputs/BattleRl/20261008_progress_shaping_controlled_comparison.json。原奖励/规则/动作/胜利标准未改，30untuned test未用，未知不判无解。无本轮代码/场景改动，不重复旧检查；先前Unity Console/全规则消费者与真实UI门禁仍待补。所有本轮进程自然terminal，无观察超时重启、上传推远端/下载依赖或终止用户程序。
