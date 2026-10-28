# CPU 优化内存峰值与恢复副本

quarantine_100 真正隔离一批4096步后继续到28672采样步，但在上游 RecurrentDictRolloutBuffer._get_samples 为 (128,6411) float32 分配3.13MiB时 ArrayMemoryError。有效更新仍5，不把采样进度或进程启动当恢复成功。旧失败日志与两批归档保留。

源码确认 main 在恢复后保留完整 torch.load continuation 的 state 局部变量；新局产生后，旧32份轨迹对象图仍被该变量引用。修复完成环境/模型/RNG赋值后 del state,snapshot 并GC。优化阶段不需要原生规则进程，因此在上游 train 前关闭本任务的32规则进程，优化和EV诊断后逐环境 snapshot/正式重放恢复，避免一次额外复制32份快照。SharedCatalogRules.close变为幂等，失败清理不遮盖原始错误。PPO数学、32环境、4096步和batch128未改。

memory.jsonl 分别保存优化前关闭规则后、优化结束、重建后全机物理/提交余量，不能将失败后的余量冒充故障瞬间测量。此修复针对可证实的重复引用和同时驻留；尚不能断言它们是所有内存增长来源。

真实第5更新的worker0快照关闭重开测试：完整current协议、观察特征和packed mask、环境RNG相等；连续close安全。隔离两个测试再次通过。新输出H:/MageKnight_RL_Runs/20261012_reward_v2_optimizer_memory_100，14:52:46 launcher168384/Python85340，从真实5更新续95，继承audit_quarantine.jsonl隔离ID，不把坏任务重新放回采样池。有效第6更新及后续连续更新需实测核验后再记录。

14:58:12 已保存真实第6更新checkpoint.zip/continuation.pt并进入behavior_0006_step24576采样。采样49.96s/优化16.17s/归档2.29s，规则重建另约117s，不能将68s当完整更新墙钟。EV更新后0.499仅批内拟合。采样时32规则进程合计private3.95GB、Python2.66GB；关闭规则后系统commit余8.67GB，优化完成余2.66GB、Python约8.62GB；重建后仅0.57GB余量，仍有较高内存风险，不能宣称全部内存问题消失。下一批优化前再次关闭规则后余量5.86GB，继续观察实际更新。

15:01:42 第7更新checkpoint/continuation已落盘，behavior_0007_step28672证明进入下一批。第二次优化后Python private约10.75GB，仍增长；为避免继续逼近提交上限，确认PID及直属规则进程后在已保存7更新边界交接。15:02:14启动nocache_100 launcher106636/Python296612，设置进程环境PYTORCH_NO_CUDA_MEMORY_CACHING=1，从同源第7更新完整恢复续93，无规则/奖励/PPO参数修改。nocache_launch.json记录环境，后续实测再判断是否释放保留缓存。旧进程仅本任务定向停止，未动用户其他程序。

15:11:15—22 nocache实际第8更新32768步checkpoint.zip/continuation.pt落盘，15:11:23 behavior_0008_step32768及RNG保存并进入下一批。采样108.88秒、优化101.96秒、归档2.51秒；优化完成到规则恢复完成另166秒左右，本批完整约390秒，不能称90秒。优化后Python private约3.36GB、系统commit余8.47GB，重建后余3.37GB/物理0.74GB，比旧轮改善但仍需监测后续增长。EV .114到.817仅批内拟合；654训练局25胜不证明冻结能力提升。
