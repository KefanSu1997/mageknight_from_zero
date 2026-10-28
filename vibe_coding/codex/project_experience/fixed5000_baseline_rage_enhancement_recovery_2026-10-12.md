# 固定5000基线费用审计与恢复

2026-10-12 00:13，PID70160自行退出，PPO更新0。固定1000验证已归档998局；worker1的episode961审计失败并阻止其后episode993，不能把998当完整分母。

失败任务3e3c2673d48a07faf3ffadd72b73d7e1430a48046c4e0d1834f1c59839967192，seed1599860165，第34索引操作原狂怒strong。红token1→0、红晶0→1；原克朗魔力强化每轮首次支付基本色后奖励同色晶体，发布真实技能标记。原审计以token+晶体净减少1判付款，漏奖励造成误报。

最小修复仅Python审计：真实Red预选、拥有技能、轮使用上升沿、精确自有红标记发布且无删除共同成立；枚举合法红token/红晶/白天金token付款，再精确核同色晶奖励及满晶溢出token。source/held保持。没有放宽免费、双奖励、错色或技能条件。原规则DLL、数据、policy、奖励和训练池未改。

原41步通过正式reset/step逐步重放，完整前后状态和raw reward逐项相等；984审计检查零错误。六负例（漏付、双奖励、缺奖励、错色、无技能、无发布）全部检出。证据：AutomationOutputs/BattleRl/20261011_fixed5000_experiment/run/eval_0000/rage_enhancement_repair_gate.json；原失败原位保存。

新增显式baseline-cache恢复入口：只在相同checkpoint SHA、固定评估种子/任务数/更新号、完整metadata和奖励配置匹配时复用无审计错误的完成档案，逐个核certificate，记录JSON/NPZ SHA，硬链接保留原证据，缺失任务以原每任务RNG重新收集。不复用失败为成功、不改变全1000分母。新增缓存缺失任务/变更checkpoint拒绝测试，与原冻结参数/RNG及失败恢复测试合计3项通过。

确认无活fixed5000训练进程后，00:18:16启动PID321832，从同initial_checkpoint.npz续入run_recovery1（原run不覆盖）；计划复用998局、补2局后进入100更新。仪表盘PID229916，指定recovery1_stderr.log，避免误显旧错误；15分钟巡检改为新目录/PID。00:19观察CPU48.8秒且活跃，仍处启动校验，不能称基线完成或训练已更新。

Unity Console两次error查询为0（仅Python工具修复，不声称新Unity场景验收）。保留完整卡牌/实体/技能消费者及Demo验收缺口。缓存重用测试不是策略能力证据；完整1000评估报告与最终曲线尚待产出。

## 00:27巡检：仪表盘写入被拒绝

训练PID321832/创建时间/完整命令行均未变，CPU已518.56秒，stderr空；仍在启动校验、无新轨迹，未重复旧规则检查。仪表盘229916于00:26:28因os.replace(live.html.pending,live.html)报WinError5自行退出，最后页面停在00:26:18。不能将页面冻结当作训练停止。

仅修改观察器页面发布：PermissionError最多短重试3次，仍失败则保留原完整页面并在下一10秒周期重试；live_status记录page_publish_deferred。模拟锁定验证旧页不破坏、解锁下一次成功发布且无pending残留。确认旧观察器已退出后启动PID34016，日志另存recovery1_dashboard_retry_*，保留旧异常日志；训练进程未重启或热换。Unity Console再次两次error0。监控提示更新观察器PID和实际stderr路径。
