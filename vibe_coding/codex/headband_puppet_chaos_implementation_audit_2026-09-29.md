# 头环傀儡与混乱：剩余基本入口定向审计

2026-09-29后续落实：傀儡已接实际公共技能与物理敌标，后台22/22、场景71操作119断言及4图通过，索引 `pve_headband_puppet_acceptance_index.json`。登记条件采用自有或击败当时真实公共供应有傀儡，不要求头环当时在手；UI支持战外保留。以下傀儡部分保留为原设计记录，不能继续解读为全部尚未实现。混乱普通借用仍待规则解释；未因此冒称整个头环全覆盖。

2026-09-29。只读生产代码、原文与既有测试；未运行 CLI/Unity，未修改生产或测试。本文是实施建议，不是通过证据。狂乱、仪式、伪装、决斗已完成，不重复。

## 傀儡：可直接推进的最小范围

原文 `resources/text_json/skill.json` 的 `hero_skill_pool_003:06`：每回合一次，保留一个“本回合中击败”的敌人，或者弃掉“之前保留”的敌人，产生其原攻击/护甲一半（向上取整）的攻击/格挡，并按原元素/相反抗性产生元素。`items.json` 的 items_022 指定非翻面每回合技能使用两次。因此独立的两次选择必须保留，不能将头环等同永久取得技能。原文没有“以前回合”的文字。

现有确切缺口：

1. `PuppetMasterActions.RecordDefeat` 第一个 guard 排除所有非持有者，因而头环使用者本回合的真实击败候选不会登记。
2. `Keep` / `Consume` 的 `RequireSkill` 强制自有引用，成功后写 `SkillsUsedThisTurn`；直接调用不是合法借用适配。
3. `Consume` 要求 `entry.DefeatedTurn < player.CompletedPersonalTurns`。这比“之前保留”更严格，使头环第1次保存、第2次立即消费不可能。自有正常每回合一次已经由使用次数阻止同回合自用二次，不需要额外年龄限制；但变更须明确为原文字面解释，不冒称新发现官方专项 FAQ。
4. `HeadbandBorrowResolution.Handles` 没有傀儡。通用 `PuppetMasterEffect.Validate` 无条件要求实际标记入口；所以当前非焚毁并不存在可玩的傀儡借用。
5. `PveBattleController.EnemyTokenSkills.cs` 已有真实候选/库存、分页、原怪物图、模式选择和借用文案。可是 `borrowed` 确认分支无条件执行 `UseHeadbandForkedLightning`，不能仅扩展 `Handles` 就算完成。

建议实现：

- 在 `HeadbandBorrowResolution` 增加 `IsPuppet`、typed `UsePuppet`，通过现有 `Use` 授权实体、首次放入已出牌区、每次扣独立 Remaining。`PuppetMasterActions` 提取共用校验/消费核心，借用路径验证 `RequireAuthorization`，不写本人技能次数，不添加 `player.Skills`，不删除公共技能实体。
- 保留/消费仍使用 `PuppetTokens` 的原物理条目与同 `EnemyTokenSupply`。库存是已经获得的实际敌标，允许两次都保留而跨回合留存；之后再有合法傀儡借用时消费，不应因当前借用结束就归还已保存标记。未保留候选继续由正式 `SkillActions.EndTurn` 的 `ReleaseUnkept` 释放。
- 战果登记必须仍在现有三个正式结果钩子中发生：`CardCombatActions.ApplyRangedRewards`、`DestroyEnemy`、`BattleResolver`，并且先于 `RecordWorldDefeat`。后者可能立即归还肆虐敌人；到 UI 打开时才拿 `DefeatedEnemyIndices` 重新 `ReservePuppet`，会遇到标记已弃置/重新抽取，不能复制敌模板或伪造库存解决。
- 最小捕获触发可涵盖持有傀儡，或当时真实手牌有 items_022 且真实公共供应有该技能，或当前相关借用。应显式记录这是“有当前合法借用可能时预留真实战果”，不要为了捕获给全体玩家永久保存所有战果。**但须保留已知边界：头环或公共技能在击败后才进入可用区的同回合路径，这种触发不会覆盖；不能把它写成原规则禁止或声称全范围完成。**如要覆盖该边界需可追溯当回合战果且供给实体仍可合法追回的额外设计，不宜本轮默默扩大供给生命周期。
- `HeadbandBorrow.cs` 打开傀儡进入原 EnemyTokenSkill；确认分流 fork/puppet。第二次仍显示保留/攻击/格挡及真实当前可选对象。`HasLegalInput` 须判断本回合候选或当前阶段可消费库存；没有合法项时复用 `FinishIfImpossible`，有项时不得跳过。
- 第一条场景选择同一战斗两名实际敌人：先用原牌杀A，再头环保留A→立即消费A的近战力量击杀B。第二种普通路径可在同条合法连续构筑的下一轮再用头环保存两个实际战果，后续借用消费库存；不需要给同玩家强塞永久技能。

限定后台建议（新增6类，加实际受影响自有测试）：

1. 非持有者、实际公共傀儡+物理头环，正式攻击击败A；候选/供给预约为同一引用，世界归还不重复。
2. 第一次 Keep、第二次 Consume 同回合A，按原值及元素产生近战并实际击杀B；头环一次出牌、公共技能还在、本人次数/技能集合不变。
3. 两个不同实际候选各 Keep 一次；正式回合/轮次库存仍在，下次实际头环可消费；不能复制已消费对象。
4. 旧库存消费作元素格挡并真实承担敌攻；半数向上取整与已有自有 `ActualResistanceSelectsOppositeBlockElement` 共用代码。
5. 同ID新副本、他人库存、未击败对象、错误阶段、第三次、另一个头环切换均在提交前拒绝，首次失败头环仍在手，第二次失败保留第一次结果。
6. 未保留候选在正式结束回合回供给一次；第二次无输入可结束、有真实合法输入不可跳过；不再有相关借用机会时不会无限预留。

既有 `PuppetMasterStateTests` 提供真实供给守恒、自有次数、多个实体、多攻击元素、相反抗性的回归。共享核心变更只选这些受影响用例；无需全卡库。

## 混乱：规则解释仍需明确，不应直接调用自有路径

原文：取得时掷骰放盾标；每回合先顺时针移动一格再得颜色奖励。轮盘顺序与奖励现由 `ChaosSkillRules` 共用：Blue→Green→Black→White→Red→Gold，蓝格挡3、绿移动1、黑冰火远程1、白影响2、红攻击2、金任选。

`ChaosSkillRules.NextColor` 强制个人已取得技能；Apply 写个人 `ChaosColor`，未使用空转写个人 `ChaosUnusedRotationPending`。`HeadbandEffect` 明确拒绝普通混乱；`PveChaosSkillTests` 的 ordinary-rejected/burn-acquires 用例仅证明当前拒绝行为，不是原规则禁止证据。

本地原文没有说明“借用不取得”的临时盾标如何初始化。此前社区汇编临时掷骰后两次顺转只是解释线索，当前没有新增官方专项原文。不得把该解释当已核官方规则，也不能借用 `HeroSkillAcquisition.Acquire` 后再移除来规避：会污染个人盾标、技能次数与取得生命周期。

若项目明确采用该组合解释，应新增仅附着当前 `HeadbandBorrowResolution` 的临时颜色，用正式 `ManaDie.RollUnrestricted` 掷骰一次；从 `ChaosSkillRules` 提取可共用的颜色转换/阶段校验/奖励应用纯逻辑。每次奖励独立选择，失败不重掷、不重复已获奖励，结束销毁临时标记，不触发个人未用空转；个人自有状态必须完全不变。

注意真正设计难点：随机初色的连续两个颜色经常跨“移动/交涉”和“格挡/攻击”窗口，不能为凑双用直接给予当前不合法资源，也不能让 Pending 强制阻止推进而软锁。先明确复合效果跨窗口剩余不可用的结束语义，再用同一个 HasLegalInput/FinishIfImpossible；第一掷骰提交前后及重试不重掷的时机需要专测。可在原高级行动现实消费者路线中选合法色，但自动化显式种子只用于可重放，不替代默认真实随机。

这一项尚缺可靠专项初始化依据；文档应继续列为解释待定/未完成，不能用普通焚毁取得已通过替代普通借用通过。没有进行新的网络重试或下载。
