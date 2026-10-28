# Tezla 057 双甲与 Defend 后台补齐

状态：代码与定向测试已准备，未运行 RulesCli/Unity；由主代理串行验收。原名望不变，阵营奖励模式仍待用户选择。

依据：`tmp/pdfs/tezla_official_rules.pdf` p2 Defend 与 p4 Elusive，及原057卡图。057原低甲1/高甲2、Defend1、两次冰攻击1、冰抗、名望1；两JSON仅新增 armor.elusive=2、defend=1，派系仍 Elementalist，错误再生不恢复。

实现：Monster 尾加 ElusiveArmor/DefendValue 默认0，Ability 尾加 Elusive/Defend，正式解析器赋予能力身份，洞悉共用选项与符号计数自然包含。EffectiveArmor远程固定高甲、近战仅正式全攻击成功格挡才低甲；零攻击正式格挡计成功，取消/仅承伤不计。所有增减甲应用于选定印刷值。Defend状态按实际来源与目标引用保存，预选不加甲或花资源，实际攻击才应用；同目标不可重复，各来源只用一次，应用后持续战斗，尚未用的已死亡来源忽略。多来源/组攻由玩家明确选择，不以ID或数组顺序分配。

UI API（主代理负责）：PveBattleSession.DefendChoices(targets)、ChooseDefend(source,target,targets)、ClearDefendChoices()、AttackArmorPreview(targets)。未决总甲预览抛需选择异常；唯一配对自动，多个配对返回列表。Context.EnemyDefense.Applied/Selected 为只读真实引用记录，Bonus(enemy)读取已应用加甲。洞悉在应用前忽略Defend会阻止来源；已应用奖励按“until end of combat”保留，本轮不宣称所有后续忽略技能组合已验证。

唯一真实六原牌连续路线：`MK.Tests.Battles.TezlaEnemyDefenseRulesTests.OriginalSixCardsDefendGroupThenBothIceSlotsBlockedAndLowArmorConsumedInFormalJourney`。Wolfhawk Lv6，010 ranged1+011 strong3蓝费+advanced000 strong火3红费；057为032中的明确一个提供Defend1，组攻7杀两原032；016/021各原物挡2实际挡冰1两个槽；advanced034 base攻击3消费全挡低甲1；同玩家正式战后结束，六原牌均弃、无伤、名望35→40→41、红蓝晶各1→0。

同文件另5个必要定向方法：原组攻与逐槽格挡；取消/实际承伤与零攻击；官方两个Defend1+一个Defend2分配及未用来源死亡；来源死后持续及高低甲同修正；洞悉能力忽略和取消预选。通用Defend2夹具明确为官方p2规则模型，不冒充现有原卡数据。

直接受影响旧测试已调整：
- `TezlaElementalistSourceDataTests.ThreeOriginalElementalistSymbolsAreMetadataNotRegenerationWithPrintedStatsPreserved`：057允许已确认Elusive/Defend，仍要求无Regenerate，名望/攻击/原低甲不变，并核新增高甲/防御值。
- `PveForkedLightningTests.GroupAttackUsesOnlyBoundEnemyCapacityAndGenericRemainder` 两参数：明确真实Defend分配，高甲总6，绑定贡献上限从错误1改为2；原强火球消费者仍成功。
- `PveForkedLightningTests.ThreeEnemyGroupUsesOriginalElusiveAndDefendArmorForActualFireAttack`：原8攻击面对真实总甲9不足，保留不击杀与不发名望的正确断言，不伪造057旧甲。
- `HeadbandTargetMovementBorrowTests.TwoIndependentForkChoicesKeepRealTargetBindingsThroughActualKills`：明确第一个Defend来源，加必要通用攻击贡献1/2以达到各自真实高甲3；两次借用与实际绑定保持。此为后台规则测试，不冒充原牌场景。

未执行整库，未修改UI/场景/生成器/共享fixture；编译和场景状态不得提前记passed。

## 首次后台与精确纠错

`AutomationOutputs/RulesState/20260929_204236_813269c0/results.xml` 实际10/11；唯一连续路线结尾误写名望40，实际41。原因是原 `basic_card_011` 利斧投掷强化原文“此次攻击摧毁至少一个敌人，名望+1”，正式 `ThrowingAxeEffect` 贡献的 `FameIfAnyDefeated=1`，与骑士精神基础3攻击无关。仅纠正预期：远程两原032名望4加原牌奖励1，35→40；近战蝠印刷名望1，40→41。补中间原奖励5断言，保留失败报告，不改生产效果。Assets再次冻结，待主代理仅重跑该失败方法。

## Unity 测试程序集边界修正

`tmp/pve_elusive_defend_compile_console.json` 保留 CS1061：新洞悉测试直接调用 internal AddInsightAbility 在 CLI 同程序集可编译、Unity独立测试程序集不可见。仅该方法改为两次独立本人合法构筑分支，每次 HeroSkillAcquisition.Acquire 后 PveBattleSession.UseSkill 正式发动原洞悉，并验证技能次数已用；不把内部方法改public、不令同一技能单轮使用两次。筛选：`MK.Tests.Battles.TezlaEnemyDefenseRulesTests.InsightIgnoresOriginalDefenseAbilitiesAndCancelledSelectionHasNoEffect`。修后Assets冻结，由主代理定向补后台与Unity编译。
