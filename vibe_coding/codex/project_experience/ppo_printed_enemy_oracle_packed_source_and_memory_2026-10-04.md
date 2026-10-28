# 原敌核心预期、Unity打包源与训练资源

原图独立预期须覆盖全部实际敌人，不只查本次撞到的ID。归纳69种原标记攻击/双甲/能力/抗性/名望/防护/召唤并保留图SHA后，发现剩余43定义差异；原012火攻/防护、原043吸血低甲、原048多攻击/笨重等证明旧内部一致性审计不足。修正共享数据时扩大到本次影响的69核心字段检查及正式消费者，共118项，而不重新跑整个卡库。

`resources/text_json/monster.json` 与Unity `Assets/GameData/BattleDemo/original_monsters.json` 是两份实际消费入口。必须同步并逐字节验证，否则后台修好而Unity仍旧。核心通过不证明所有阵营奖励、名称/类型或UI交战。

训练启动前检查实际内存和提交余量。Unity多个进程不一定是多编辑器：以父PID和AssetImportWorker身份判断。通过官方AssetDatabase管理空闲导入工作进程，记录原会话参数，不直接杀用户进程、不重启机器、不改虚拟内存。API可能引起Unity后续自动新建工作进程，重新测量实际余量再启动，不能仅凭成功返回做判断。

新原数据域与六目标布局用独立v38训练目录和真实PID/创建时间核实。核心JSON/原预期/打包资源哈希纳入manifest。初始化基线无stdout时观察同一活进程，不重复启动。详细证据见 `vibe_coding/codex/worklog_2026-10-04_original_enemy_core_and_v38_training.md`。
