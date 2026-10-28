# 共享技能实际回合与移动/行动边界

- 原互动技能的合作面通常在side1_skill.total_description，不能只读空根字段推导Unspecified。必须核实际loader返回和原图，不把“在JSON看到每轮一次”当作运行时已加载。
- 一局共享区保存真实主人、真实SkillCard与领取标记；实际GameEngine当前玩家/同一context验证，轮流之后旧context拒绝。已学中央标记不同于未学公共技能供应。
- 自然复仇与魔力强化中央标记到主人下一回合开始；痛苦仪式/风雨平复不能套这个短期限。自然复仇授予敌人的减攻及笨重持续本回合，支付移动减攻仅当前战斗，生命周期应分开。
- 笨重格挡阶段1移动减1攻击，减到0算成功格挡，绑定实际敌引用及攻击槽。同名敌人不能值相等合并。
- 普通旅程入战/地下城从移动阶段开始行动，先前剩余Move/Influence丢弃，但整回合转换许可保留；地道附接等已进入行动的入口不能再次清池。不要在所有BeginBattle无条件清，迷宫在行动内生成的剩余Move合法保留。
- 回归发现六旧灵动/外交场景夹具错误跨阶段带点，应纠正出牌时序并保留所有费用/伤牌/消费断言与失败报告，不能为使旧证据继续绿灯取消规则修复。受影响Unity后段重放补验，无须重测原卡全库。

本批59有效后台通过，灵动场景49增量步95断言通过，4关键图已看、Console双0。共享三技能仍后台状态，七互动场景入口未完成（63/70计数不变）。证据：vibe_coding/codex/pve_shared_skills_foundation_index.json；详细：vibe_coding/codex/worklog_2026-09-29_shared_skills_foundation.md。规则来源与迷宫例子：tmp/nature_revenge_movement_boundary_audit_20260929.md。
