# PPO 爆裂箭矢真实后续入口与魔力宣夺复合付款

2026-10-05，原 v79 失败与新增入口独立保留。

- Norowas seed305609：原爆裂箭矢强化击杀064产生减甲义务；正式 Advance 拒绝“请先选择或放弃爆裂箭矢击杀后的减甲”。RL 没有对应动作，其他可用动作耗尽后 no_legal_continuation。不能删除正式义务或把终止当作正常战斗结束。
- v80通过正式 PveBattleSession.ResolveExplosiveArmor 接入六个实际敌人目标与放弃。公开剩余次数追加至观察，新 schema；按完整动作定义/Key、旧输入坐标和类型名迁移4096真实检查点，新增输入与动作头零初始化。旧动作投影概率保持不代表新增合法动作后的完整策略保持。
- 原失败前缀重放后实际存活目标0/2以及放弃三分支均消耗正确次数并恢复进入格挡；死敌与越界动作不合法。四个负例检出。减甲后后续攻击消费者及最终 Unity 游玩验收仍需补足。
- v79另一个真实 seed308630 第40步：魔力宣夺蓝骰，同时极化用实际源骰 White 支付原蓝费；源池 White/Black/Blue 变 Black，White成为Held，Blue成为 roundClaimedDice 且次回合Blue收入+1，本人极化次数消费。审计不能只要求移走 Blue 一颗，也不能放宽成任意差集：必须逐颜色精确核对 Blue宣夺+实际White付款。59操作完整前后状态/奖励重放一致；缺失任一消费的两个负例均检出，原失败文件保持。
- 后续减甲记为消费者而不是再次发动原卡，首次真实消费者轨迹保存本地。原 v79 PID已自动退出后才续训；4096之后未保存更新不冒充恢复。新桥接DLL与旧DLL不同，训练历史按既有门禁分开，不伪造旧manifest哈希绕过继承保护。

证据：AutomationOutputs/BattleRl/20261005_v80_explosive_checks.json、20261005_v80_explosive_migrated/migration.json；实际Console两次零错误 tmp/rl_v80_explosive_followup_final_console_20261005.json。后台构建0警告0错误。正式规则、卡牌定义、Unity场景无修改；通用胜率和全范围最终验收尚未完成。
