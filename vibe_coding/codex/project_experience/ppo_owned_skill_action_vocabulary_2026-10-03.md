# 原技能训练动作按真实入口生成

PPO 动作目录不应给每种技能统一枚举0到5、100、200等编号。多数编号没有该技能的正式含义，虽会被合法掩码排除，仍扩大策略输出并浪费更新算力。动作应来自 BattleSkillChoice.For 的真实选项，只对社会技能、混乱、洞悉等动态入口补充它们各自的正式编号。

观察保持旧特征前缀，在末尾追加原技能持有及真实回合/轮次/翻面标记。旧模型按动作 Key 匹配迁移，不能按数组下标猜测兼容。使用 SkillActions 的正式入口，技能取得与实际发动分别计覆盖；需要牌、部队、地图和共享区额外输入的技能明确保留缺口。

证据：tools/BattleRl/JourneyTraining.cs；tools/BattleRl/train_ppo.py；vibe_coding/codex/worklog_2026-10-03_ppo_owned_skills.md。
