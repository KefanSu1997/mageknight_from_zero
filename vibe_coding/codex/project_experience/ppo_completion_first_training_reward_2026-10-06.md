# 整场胜利训练回报与原规则回报隔离

v86/v87真实评估有部分击杀却无整场胜利，累计回报仍正；无击杀终止条件不能代表整战目标。降低价值损失后的v87固定/一次采样均0/7，不能继续把加时当解决方案。

采用显式 `--completion-first-reward`：只在PPO批次中将击杀奖励10减为2，以无正式整战胜利终局-10替换无击杀终局-5；原整战上升沿+20和其他项保留。正式 `End && won` 上升沿记录局内历史，跨回合保留、reset清空，不用最终状态单个won标志。开局正式fight课程前缀不进入PPO回报。保留原 `reward`，另存 `training_reward`，默认关闭完全保持原回报。严格要求原完整journey回报契约，禁止对pilot猜测替换。

4项检查通过：未胜击杀/终止保留其他惩罚、正式胜利跨回合与reset、非End误标拒计、原实际前缀不变。原seed391825的24次操作真实重放：原状态/原回报精确一致，整观察与完整合法掩码未被shaping修改，正式推进End才记胜。证据 `AutomationOutputs/BattleRl/20261006_v88_completion_reward_actual_prefix.json`。Unity Console两次零错误 `tmp/rl_v88_completion_reward_console_20261006.json`。未改Unity场景或原规则，不宣称新的场景通过。

v88有界16384步，仅奖励实验开关改变，保持原全范围/同DLL/原要素覆盖守卫和评估。起始权重与Adam恢复情况见launch；奖励候选通过不代表学会战斗，全部原消费者/7英雄/69敌人/最终保存策略真实Unity验收继续独立。
