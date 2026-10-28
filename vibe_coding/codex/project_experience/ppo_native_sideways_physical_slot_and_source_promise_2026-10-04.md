# 原技能横置必须绑定物理手牌与整回合源骰历史

普通 UseSkill 不带卡牌、付款与用途，无法代表漫不经心、魔法抗拒、通用力量。训练动作应调用正式 PlaySidewaysWithSkill，选择实际 handOrder 槽，观察整回合真实取骰历史及拒绝取骰承诺，预检失败不得消费原技能。

攻击贡献 SourceCard 是实际横置牌，不能据卡 ID 将其归类为印刷基础/强化效果。按物理实例保存当时原技能/模式，再对应后续实际消费；生成值和消费证明分开。

全部手牌是伤牌时，打完一场只用技能/部队的战斗仍可能不满足最低出牌/弃牌规则。保留正式拒绝及 seed，不通过自动休息、自动弃伤或放宽结束回合来消除失败。

验证与限制见 ../worklog_2026-10-04_ppo_native_sideways_skills.md，实际证据保留在 AutomationOutputs/BattleRl/20261004_native_sideways_skills.json。
