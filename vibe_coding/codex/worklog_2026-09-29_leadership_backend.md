# 原领导技能后台（2026-09-29）

## 原则与接口

复用原 `hero_skill_pool_002:06` 领导与原部队能力；用户先选择实际自有部队和能力，再一次确认技能与部队激活。新增 `LeadershipActions.Use(player, context, skill, unit, abilityIndex, option=0, target=-1, manaColors=null, boost=0, attackIndex=0)`；`CardUnitActions.Use` 尾部可选 `leadership` 参数供正式场景复用。

费用前校验实际持有且本回合未用技能、真实就绪部队、能力选择与战斗阶段。格挡 +3、原近战攻击 +2、原远程攻击 +1，原攻城拒绝。加值直接加在这次 `ApplyAbility` 原元素贡献上；保留原红蓝 boost 付款、耗竭、自伤、荣誉奖励、SourceUnit。仅正式能力成功后记技能次数，不产生全局悬空加值。部队效果范围清空当前卡牌来源，供应借能力也不冒充原卡贡献。

## 验证状态

新增 `PveLeadershipSkillTests` 8 项：原阿尔蒂姆法师四种正常/红蓝 boost × 攻击/格挡，原火法师远程真实击杀，冰攻城/非贡献拒绝，阶段/选项/外来技能/缺费用守恒，剑士自伤与其他部队不继承及一次性次数。主代理统一串行 RulesCli 后，`AutomationOutputs/RulesState/20260929_040049_bf6cd389/results.xml` 中本 fixture 8/8 通过；本代理已逐条读取此实际结果。

Unity 场景、Console 与整体汇总由主代理负责；后台证据不能替代场景验收。本代理未操作 Unity、网络、远端或系统配置。
