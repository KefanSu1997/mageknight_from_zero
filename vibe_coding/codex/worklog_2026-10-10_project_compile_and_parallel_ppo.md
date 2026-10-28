# 2026-10-10 项目编译修复与双环境 PPO

## 用户目标

修复项目报错，并继续可用于 Demo 测试的 PPO：增加逐步时间代价、公开原卡费用/效果信息、多环境采样，自行打开 Unity 验证。最终原要素和自主 UI 验收仍未完成。

## Unity 实际编译

经 Unity Hub 用 Windows 绝对项目路径打开 6000.6.0f1；不直接调用 Editor、不使用 batchmode、不修改安装包。实际 Editor PID 241640，创建时间 2026-10-09 23:40:33 +08:00。项目日志重定向至 Logs/Editor.log，旧全局日志不代表此次会话。

Safe Mode 的 CS0200 位于 Assets/Tests/RulesState/OriginalMapCheckpointTests.cs:20：测试程序集不能给 OriginalMapCell.TerrainName 的 internal setter 赋值。RulesCli 同程序集编译此前未暴露错误。移除非法赋值，保留正式地图容器清空/Undo、物理 cell/collection 引用、21 原格/18 已放置格断言，并断言没有 public setter；不放宽生产属性。

初次 CanWrite 断言错误（internal setter 仍使 CanWrite=true），失败保存于 AutomationOutputs/RulesState/20261010_011730_296c2436/results.xml。改为 GetSetMethod()==null 后，受影响测试 1/1 通过：AutomationOutputs/RulesState/20261010_013114_cc87663b。

复用已缓存本地 MCP server（uvx --offline，关闭遥测），不下载依赖。Assets/Editor/CodexLocalMcpStartup.cs 只处理显式一次性本地 request，用已有菜单启动 HTTP bridge；普通 Editor 加载不启动。现有官方 Unity CLI 未发现 Pipeline 连接，未安装 Pipeline 包。MCP refresh 已返回 editor ready，两次实际 read_console types=[error], count=1000, include_stacktrace=true 均 success=true、data=[]：AutomationOutputs/UnityMcpLocalRuntime_20261010/console_after_fix_1.json 与 console_after_fix_2.json。这是实际 Console 编译门禁通过，不是全部 Demo 运行验收。

## PPO 新链路

- 三层 MLP256/256/128 + GRU256 + 实体 Transformer 保留，公开印刷文本通过共享参数描述进入动作头。新增主实体/公开 Input 引用实体各 128 维，旧 96 维保留，总 352。字符/字符对特征不是预训练语言模型，不凭来源 hash 宣称理解规则。
- 从真实 1136 局/64358 步/Adam345 检查点暖迁移，117 参数/动量数组保留前缀，新文本行与动量零初始化；六个实际正式操作完整合法概率/值/GRU 差为零。证据：AutomationOutputs/BattleRl/20261010_printed_input_warm_binding。
- 额外每步 -.02 训练代价，原环境 reward 与动作/原奖励历史不变，终局目标不变。仅参考序列证明更快成功优先，不保证消除循环。
- parallel_rollouts.py 实际启动独立 .NET 规则进程，独立 worker RNG/策略可变缓存/RNN；同一冻结 actor 完成并行采样后再同步 PPO 更新。所有候选按原混合池概率选取，不以失败判无解。批次先选任务、结束后按 episode 顺序更新 sampler，明确区别旧逐局立即更新。
- 两环境试运行 PID88216（01:41:31 +08:00），已正常结束，不重复启动。实际环境 PID316744、286172。243候选=25获胜见证+218未知，学习率 .0001，1 update×2 episodes×1 epoch。144 步、1完整任务成功、1未知任务失败，新增审计3476检查零错误；训练更新阶段31.58秒不当端到端时间。最终1138局/64502步/Adam346。
- 39 参数数组均更新，公开文本新增行8160个非零，最大绝对值0.0001709634571776187；保存两个不同 worker RNG，完整 checkpoint loader 成功恢复参数/Adam/父 RNG/边界记录。证据：AutomationOutputs/BattleRl/20261010_printed_step_parallel_smoke/saved_update_verification.json。连续训练与断点恢复逐轨迹等价尚未新验；冻结胜率尚未新验。
- 三项针对性数值/隔离/RNG/目标测试通过。试运行结束后补修 serial checkpoint 的 collection_workers 严格恢复字段，并让未来轨迹保存 worker_id；旧两条轨迹不回填该字段。

## 尚缺

## 用户要求扩大并行采样

用户明确指出双环境不足，要求8/16/32规模。实机32逻辑CPU，但启动前空闲物理内存524260 KiB；单份actor参数86.66 MiB。未关闭Unity或用户其他进程。先从真实1138/64502/Adam346恢复启动8独立环境、8局、1同步更新/1epoch的规模探测，保留原任务池、学习率与步数目标；改变worker数量显式--change-collection，不把重新派生8条worker流说成原2条流连续不变。训练PID261092，创建2026-10-10 10:13:28 +08:00，完整命令可核验，输出AutomationOutputs/BattleRl/20261010_printed_parallel8。16/32待实际吞吐和内存证据再推进，不声称已启动或完成。

后续状态核实：本轮已于10:16:55正常保存结束，8个真实环境PID与worker0–7轨迹均记录，8个不同任务、525步、1整任务成功、7失败（6个初始可解性未知，不判无解）。12175有限规则审计检查零错误。最终1146局/65027步/Adam347，全部117数值数组finite、8独立worker RNG保存。采样及更新阶段56.68秒，启动到检查点约206秒，二者分报；不同任务/步数不直接当双环境提速倍数。状态查询时整机空闲84968 KiB（约83MiB），系统pagefile当前3319MiB/峰值7439MiB是整机量，不归因该训练。16/32尚未启动，须降低复制/缓存开销或获得内存余量；能力独立冻结评估未完成。不把1/8训练成功当泛化胜率。

冻结能力对照、循环改善、多环境准确续训对照、全部原卡费用与后续消费者、未见构筑测试、保存 policy 自主 Unity UI 操作仍未通过。此次编译错误已修复；训练未宣称可用策略完成。
