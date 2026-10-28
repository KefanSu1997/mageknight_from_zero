# 头环借用决斗：同敌同攻击双效果后台

2026-09-29。后台已通过；本文件不代表 Unity 场景通过，主代理继续负责 UI / 场景 / Console。

## 修改

- `DuelActions` 抽出共用真实目标校验和效果提交。自有决斗仍为格挡 1、后续同敌攻击 1、无部队介入击杀名望 1；借用通过内部授权提交强度 2。部队历史、实际敌人引用、原攻击槽、后续近战自动授予及原击杀奖励全部复用。
- `HeadbandBorrowResolution` 新增 IsDuel / UseDuel：真实手牌头环和实际公共非互动技能校验后，一次原子完成两次同敌同攻击效果，头环实体仅入已打出一次，无残留第二次输入，不修改个人技能持有或个人使用次数。
- `PveJourneySession.UseHeadbandDuel(card, skill, Monster, attackIndex)` 限制实际本人回合、当前战斗格挡阶段及实际敌人引用。另拒绝 `Battle.IsBlocked`，因为预定格挡先存战斗 Session，正式推进防御阶段才写入 SuccessfullyBlockedAttacks；只检查 Context 会开放已经处理的攻击。
- 没有把两次借用展开成两名敌人的独立决斗，也没有临时授予自有技能。

## 验证

新增 `HeadbandDuelBorrowTests` 共 10 项，直接受影响 `PveDuelSkillTests` 10 项，最终有效 20 / 20：

1. `AutomationOutputs/RulesState/20260929_144936_8967a9d0/results.xml`：18 / 19。失败为测试调用原决心默认选攻击，未明确选择格挡分支；生产效果未改变。
2. `AutomationOutputs/RulesState/20260929_145031_e0c66234/results.xml`：仅补失败参数 False，1 / 1；原决心选择 option 1，真实格挡、后续原愤怒攻击、实际击杀奖励正常。
3. `AutomationOutputs/RulesState/20260929_145119_ce22d8e0/results.xml`：新增 Session 已预定格挡守恒拒绝，1 / 1。

主要证据：头环已打出实体；公共技能保留；自有列表与使用次数不变；无 Pending；同名其他敌人不能消费定向格挡；正式格挡后近战贡献仅生成一次；原愤怒配合击杀取得 2 名望奖励；实际部队格挡取消该奖励；错误阶段、外部同值敌人、无效槽、已死 / 已挡、供应外技能、伪造头环均拒绝且保留手牌、贡献、技能和名望。

原始失败报告保留。未运行全库，未操作 Unity，未修改 UI / 场景生成器 / AGENTS / current_goal，未联网或推送。
