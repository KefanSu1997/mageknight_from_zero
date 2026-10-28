# Recurrent PPO学习率配对与保存策略双战成功

问题：完整KL约1e-6、类型门概率近均匀，类型优先可单战胜利但双战反复承伤规划。新增可选--learning-rate；完整恢复后仅改Adam学习率，保持参数、first/second、step和RNG，源模型不覆盖；manifest登记原/新数值及实际optimizer设置。默认不变，非法零/负/非有限拒绝。

从同一46episode1977步Adam32检查点分两支：基线5e-5、实验2e-4，各两更新×5episode×4epoch。第一批5条实际操作链完全一致，更新后首完整KL1.174e-6对1.859e-5，约16倍；后续动作链有差异，不声称全部十条相同。两支各新增10局432步、2完整任务、累计56局2409步Adam40；变化不等于效果保证。

相同五训练组成冻结类型优先校准：基线3/5，实验4/5。Goldyx实际33步连续击败049→050，两个正式End胜利上升沿、两次正式EndTurn收尾，跨回合/第二fight GRU未清；来源前后状态保留，参数/源检查点不变。Krang仍256步规划死路，6整组胜利不能冒充6完整任务。只证明一次局部保存策略成功，不外推held-out、跨昼夜获胜或全原规则。

额外原开局任务探索是既定全范围组成：Tovak/Wolfhawk真实原16牌对002，各35秒1241/1272调用unknown，不把失败改成不可解或删除中期难任务。搜索原起手未补单位/晶体，不将原开局替换为有利自造牌库。

证据20261007_recurrent_learning_rate_pair_report.json、20261007_recurrent_lr_pair_baseline/4x、20261007_recurrent_lr_baseline_hierarchical_eval、20261007_recurrent_lr_4x_hierarchical_eval、20261007_discovery_original_opening_missing_heroes。两项学习率保历史/非法负例通过，Console两读0错误tmp/rl_learning_rate_pair_console_20261007.json。所有本轮进程已正常完成；新旧候选均保留，目标尚未完成。
