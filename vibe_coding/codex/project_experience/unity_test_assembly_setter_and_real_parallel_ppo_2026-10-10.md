# Unity 测试程序集 setter 与实际并行 PPO

RulesCli 与 Unity 编译程序集边界不同。同程序集 RulesCli 能访问 internal setter，不代表 Unity Tests 程序集能访问；本次 CS0200 在 OriginalMapCheckpointTests 对 TerrainName 赋值，导致 Safe Mode。保留原生产 public-get/internal-set 合同，测试实际可变容器恢复和物理对象身份；验证无公开 setter 应用 GetSetMethod()==null，不能用 CanWrite==false。原错误预期失败不覆盖。

必须以实际 Console 两次零 error 确认编译恢复。本次 Hub 正确 Windows 路径打开 6000.6.0f1，项目 Logs/Editor.log 为实际日志，旧全局 Editor.log 是过时文件。复用 offline 缓存 MCP，以显式 one-shot request 调用已有 bridge 菜单后，refresh ready、两次 Console success/data=[]。官方 CLI 没有 Pipeline 连接不等于没有其他本地桥；不安装包、不使用 Editor batchmode。

多个后台实验并非多环境 PPO。实际同步采样需要不同原规则进程、不同 RNG/RNN/策略缓存；共享动作头内部会临时修改参数，线程不能共享同一个 policy 实例。采样完成前不更新 actor，批次选样/完成后 sampler 更新顺序须明确；checkpoint 保存每个 worker RNG，缺失不能静默重新种子。新增串行 collection_workers 字段也要加入严格 metadata 恢复预期，不能仅在并行 SHA 存在时加入。

印刷文本暖扩展需保留旧描述行/参数/Adam 与 RNG，新行零初始化，并用实际完整合法概率/值/GRU 验证迁移。真训练新行非零只证明梯度实际更新，不证明印刷含义理解。额外步数惩罚保留 raw/history，参考序列快胜优先不保证所有状态消除循环。

本次实际双环境2局144步，1胜1未知失败，3476审计检查零错误；117保存数组与2 worker RNG可读取，39参数数组更新，新文本行8160非零。只有一次 update，不当冻结能力提升或 Demo 全规则/UI验收。详细记录与证据见 vibe_coding/codex/worklog_2026-10-10_project_compile_and_parallel_ppo.md。
