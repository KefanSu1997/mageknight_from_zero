# 32环境与128局batch

用户连续要求提高batch到128并优化速度，再将并行环境提高到32。按前文完整episode口径配置32个原规则环境、每worker4局，共128局rollout/update；最多4epoch，计划100更新。旧8环境100更新仍活跃（核实PID86980/2026-10-11T18:05:32），不热换或重复启动。

queue_parallel32.ps1 已启动等待器PID176520/2026-10-11T19:13:05。只等待精确旧PID/启动时间，旧进程退出后要求progress.update==100并无其他recurrent trainer，随后恢复最后完整checkpoint，以--change-collection显式改32worker；旧8worker随机流不声称原样延伸至32worker。若旧任务提前失败则保存blocked原因，不盲目重启。队列状态：AutomationOutputs/BattleRl/20261011_parallel32_batch128_updates100_launch/queue.json。尚不能称32环境实际运行通过。

速度改动仅对下轮生效：在首次numpy导入前设置BLAS/OMP/MKL单线程，避免32采样线程与BLAS内部线程叠加；原设置位于train_ppo中，可能晚于recurrent入口的numpy导入。梯度加权在独立临时梯度上原地乘，省每episode一个参数大小的临时数组；独立16数组实验结果逐元素完全相等，测时0.0239→0.0081秒，只证明局部累加，不能外推端到端。新增collection/optimization/checkpoint分段计时和worker/预算元数据，曲线标题从实际配置读取。py_compile通过。

当前实机16GB，8环境trainer工作集约2.31GiB、规则worker每个约85–88MiB，空闲约1.76GiB；32环境可能受内存和Python开销限制，必须按实际吞吐判断。此次无Unity C#修改，前轮MCP端口拒绝限制仍未解决，不宣称新的Console双零。未关闭用户程序或更改活训练规则。
