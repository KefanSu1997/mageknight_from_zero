# PPO 欠覆盖采样及连续计划成本

本轮前一goal turn为progress：新增真实附旗消费者并启动v19。当前核实实际训练PID209568仍活跃，冻结v19DLL；不重启，不并行第二训练器，不把当前源v20算进正在跑的批次。

## 实际轨迹发现

v19到4096步/27episode，7英雄21原敌22部队、115模式29输入22技能选项36部队能力78消费者。固定七英雄小样本5/7清至少一组、9完整胜场、平均203.57步。实际保留7轨迹1223操作、8完整胜场/24启动战斗、3超时、最长连续计划236。目标未达成。

seed3376 `audit_2048_1.json` step91—102连续12次切换防护清理选项。原规则允许反悔，不能删除合法操作来造胜率，但当前奖励不足以避免循环。保留原轨迹SHA bca15fb383bae9bf7e1cd6e61c6cb85478bd5829a6d39e5954c3f64fbcc2d98d。

## 新环境与验证

`JourneyTraining.cs` v20只增加训练状态PlanningRun及对应观察。连续预选（承伤、defend/clear、弓、击杀色、强韧、防护清理）每次额外成本 `0.01*min(连续次数,32)`；真实出牌/攻击/格挡/回合等操作清零。所有合法分配、取消、反悔仍存在，原伤害/费用/奖励不改。奖励定义显式写入protocol/manifest，PPO不同奖励版本不可当相同训练条件比较。

`verify_planning_cost.py`冻结新DLL精确重放seed3376全部132操作：每步原游戏状态完全一致，原操作仍合法；72计划操作的奖励仅减少声明成本，计数/观察正确，现有规则断言通过。`20261003_planning_cost_exact_replay.json`。这是奖励一致性与规则不变证明，不是训练拖延改善证明。

v20冻结DLL `tmp/battle_rl_v20_planning_cost/BattleRl.dll` 编译零警告零错误。Console两次零错误 `tmp/rl_planning_cost_final_console_20261003.json`。没有场景代码改动，Unity策略验收仍另做。

## 欠覆盖采样

新 `coverage_sampler.py`、train_ppo `--coverage-window 21`：只读inspectInventory查看21个后续合法种子的可见手牌/自有技能部队/初始敌群，优先当前批少选英雄、其次原要素少实际使用的候选。只选构筑，不强迫动作，不读取隐蔽未来牌序。使用次数只由实际成功play/artifact/skill/unit/bannerUse/cleanup增加；formal拒绝不算，取得或进入手牌不算原效果消费。真实效果消费者仍另查，模式字段包括原选择与输入。

`verify_coverage_sampler.py`18项验证：七次选择覆盖7英雄各一次、每次保留原正在跑的完整state/features/legal；探测不计使用，原真实恐惧附旗/使用两次计数，formal拒绝不计。`20261003_coverage_sampler.json`仅采样器定向证明，不算PPO训练覆盖。

选择过程保存 `curriculum_selections.jsonl`，真实使用计数 `actual_use_curriculum.json`。旧连续种子默认保持兼容；manifest保存采样器SHA与window。训练覆盖补记真实旗消费者；当前v19进程已加载旧Python，新增计数/采样均要下一批才能启用。

`review_policy_behavior.py`新增防护清理及强韧预选统计；修复一次局部变量遮蔽参数导致的报告异常，实际4096报告已正确生成，不改轨迹或规则。

## 下一动作与全范围缺口

等待并核实v19实际exit后，用其最终policy_ppo（失败则最后有效checkpoint）及metrics.next_training_seed，顺序启动冻结v20：先1024步烟测，带 `--coverage-window 21 --audit-seconds 60 --eval-episodes 7`；通过新实际轨迹和模型迁移后再长批。不得使用当前源码/新DLL去冒充旧版本失败精确重放。

目标仍是全部原要素、全部英雄与战斗、合法多构筑、多回合/昼夜的通用PPO测试模型。头环/宝盒等复杂输入、全技能、真实地图场地、旗取回窗口、全部实际覆盖、未见构筑评估及最终Unity回放仍有缺口。无任何全目标完成声明。
