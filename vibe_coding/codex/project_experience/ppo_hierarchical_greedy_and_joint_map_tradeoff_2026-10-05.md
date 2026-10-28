# PPO层级贪心与完整联合动作解码的实际对照

v68实际模型SHA以 `AutomationOutputs/BattleRl/20261005_v68_actual_planning_probability_credit.json` 为准，不沿用v67SHA。

实际seed200000 Norowas137步/274前后状态及reward精确重放。第51步承伤规划类型概率0.522，advance0.478，层级贪心选中具体规划动作概率仅0.045。先选类型再选参数不是完整联合分布的MAP；74步调整计划累计奖励-19.46。保存critic离线全局GAE为负不能冒充原训练rollout优势，也不证明优化器错误。

保持权重、正式规则、合法掩码和完整概率分布不变，临时按np.argmax(full_probability)评估配对200000..200006：最长规划0，仍仅Norowas1/7、4英雄未战斗。固定100000..100006全部0/7，原层级贪心Goldyx/Wolfhawk2/7的胜利丢失。因此消除一个循环不是整体策略改善，不能直接热换正式解码或宣布模型可用。

原PPO全概率随机采样（RNG32768）在配对7局实际5英雄进入战斗，仍1/7，最长规划3；只一次采样，不能外推稳定胜率或掩盖no_legal失败。

证据分别在 `AutomationOutputs/BattleRl/20261005_v68_jointmap_fixed_diagnosis/summary.json`、`20261005_v68_sampled_paired_diagnosis/summary.json`。固定采样结果另行记录，不能预先猜测。

后续：推断解码、学习概率质量与规则正确性分开。保留原失败；正式奖励/规则没有在此修改。下一步依据原保存策略实际战斗决策和收益进行诊断，不能通过强制进战、删除合法规划或降低验收标准隐藏问题。

固定原PPO采样100000..100006完成：7英雄均实际进战，但仍仅Wolfhawk1/7，最长规划4。证据 `AutomationOutputs/BattleRl/20261005_v68_sampled_fixed_diagnosis/summary.json`，仍只一次采样，未证明可用。
