# 特殊付款完成后的部队剩余范围：只读审计

**后续实际结果（20:12）：非白基础影响3缺口已补。** pve_familiar_base_influence_acceptance_index.json：后台195848唯一方法37断言；场景200051_5d478abe新增65步120断言，同红调谐魔宠影响3买治疗并移除实际旧伤，四图已看、Console双0。下文该项为历史审计，不再重复运行；其它基础参数边界及Core依赖保留。

本次只读原定义、现行代码、已知小范围索引和两份确切 XML；不改 production/ledger/fixture，不运行测试或网络。数值参数复用与独立场景消费者分开，不把后台结果改称场景通过。

## 魔宠：合法色标下的非匹配基础值

原来源 `resources/text_json/army_2.json` army_2_004 及 `Assets/GameData/cards/army_2_004.png`：攻击3（红+2）、格挡4（蓝+3）、移动/影响3（白+2）、治疗2（绿+1）；招募付基本色，轮初付库存魔晶替换色标或移除。这里没有“无调谐免费魔宠”合法起点。

`Assets/Logic/Runtime/FamiliarActions.cs:13` 为唯一数值选择：`ability.Value + (unit.AttunementColor == ability.AttunementColor ? ability.AttunementBonus : 0)`。`CardUnitActions` 对攻击、格挡、移动或影响、治疗使用这个实际值，区别只在对应资源池/消费者。不能为了测3/4/2直接清色标。

| 原范围 | 已有有效证据 | 最小处理 |
|---|---|---|
| 红攻击5、蓝格挡7、白移动5/影响5、绿治疗3 | `pve_familiar_consumers_acceptance_index.json` 两路线，同招募单位、实际费用/维持/消费者 | 已过，不重测 |
| 非白移动3 | `20260929_122149_cee7cf11/report.json` 90→97红色标、114→119蓝色标，真实移动支付 | 已过，说明共享“不加奖励”分支确已场景消费 |
| 非红攻击3 | `20260923_025452_e400c660/results.xml` `MagicFamiliarsStateTests.AttackUsesPaidMarkerAndActualEnemyConsumerWithoutAnotherManaCost(White,3)` **Passed** | 保留确切后台及共用数值分支证据；暂无这个值的直接场景消费者，不重新跑后台参数 |
| 非蓝格挡4 | 同 XML `BlockMarkerFeedsFormalBlockAndPreventsActualWounds(Red,4)` **Passed** | 同上；原032真实攻击4已后台消费 |
| 非绿治疗2 | 同 XML `HealingPaysForRealWoundCardsAndDoesNotHealTheUnitByReadying(Blue,2)` **Passed** | 同上；真实伤牌/单位等级已后台消费 |
| 非白影响3 | 同 XML 只有 `WhiteAbilityChoosesMovementOrInfluenceAndSpendsItOnRealConsumer(1)`，该方法招募白魔宠并明确断言5；不能冒充影响3 | 若补精确基础值，优先新增一个非白合法招募魔宠→影响3→实际村庄治疗价3的独立后台消费者，再纳连续场景 |

`WhiteAbilityChoosesMovementOrInfluenceAndSpendsItOnRealConsumer(0)` 与 `(1)` 均 Passed；前者同样是白5，不是基础3。上述 XML 中红5/蓝7/绿3参数也Passed，已被现有场景补齐，退出执行队列。

**哪些是参数范围，哪些需独立消费者：** 不加奖励的判断已由红/蓝移动3的实场景覆盖，四个能力入口及各自后续消费者已由对应强化色场景覆盖，攻击3/格挡4/治疗2已有真实后台参数消费。因而不能把这三种纯数值组合各扩展为新的完整验收批次。可以明确记录“共用分支场景＋对应后台参数”，但仍不能写成该精确值直接场景通过。影响3缺确切独立后台参数，若继续追求每个基础数值的直接场景，应集中在同一合法旅程补四项，不重做招募/四色强化/完整旧批次。

最小可行组织：复用原林地真实招募前缀；同蓝调谐魔宠可依次覆盖攻击3、影响3、治疗2，必须通过正式跨轮维持或实际原牌重整恢复耗竭；再以库存红晶正式换色覆盖格挡4。每次激活都保持同单位，真实伤牌与费用来源需在路线中积累；不得手动ready、补伤、增库存。若只补影响3，直接沿用已过红调谐招募起点，下一回合产影响3并在真实村庄消费即可，前缀不再截图。此处是组织建议，尚未生成或执行新路线。

## 勇者：整次交涉声誉修正翻倍

原规则来源沿用已完成的 `worklog_2026-09-23_heroes_recruitment.md`：当时核对 Lost Legion 原扩展规则第5页，招募费用旁特殊标记使**整次交涉的冻结声誉修正翻倍**，正负都适用，多支只应用一次，同次交涉不能同时招募暴徒；非交涉免费取得不触发。本轮未网络重新下载，不把旧来源审计说成本轮重新目视PDF。

当前四ID army_008/009/010/011 都有 `double_recruit_reputation:true`，等级III、印刷费用9、招募地点**村庄/要塞/城市**。不是法师塔。`UnitJsonLoader` 为四者提供同一个 `DoubleRecruitReputation` 字段，同时因 `army_`归为精英牌。`RecruitmentService.InteractionModifier/GetCost/TryRecruit` 使用开始交涉冻结修正，首次招募勇者只补记差额并设置 `InteractionDoubled`，以后同交涉不重复翻倍；失败、暴徒互斥先拒绝。

确切有效后台 `AutomationOutputs/RulesState/20260929_052835_8f50a877/results.xml` 中以下全部 **Passed**（只读现成结果）：

- `HeroesRecruitmentTests.TwoHeroesDoubleFrozenWholeInteractionModifierOnlyOnce(2,16,2)`、`(-2,20,-2)`。
- `OrdinaryRecruitBeforeOrAfterHeroHasSameInteractionTotal(False,2,9)`、`(True,2,9)`、`(False,-2,5)`、`(True,-2,5)`。
- `HeroInfluenceAbilityChangesReputationWithoutRepricingTheInteraction`。
- `HeroesAndThugsCannotBothBeRecruitedInOneInteraction(False)`、`(True)`。
- `FailedRecruitDoesNotDoubleModifierOrConsumeOffer(0)`、`(1)`、`(2)`。

这些为真实服务消费者，但测试显式提供原勇者 offer，不是当前可游玩地图自然供给的场景。四ID共享规则只需未来一个实际精英招募代表消费者，不应四颜色重复。场景需非零冻结声誉修正，否则翻倍0不能证明实际差额。

## 当前真实精英供应不可达：与原核心板块资料同一阻塞

已核完整触发链：

1. `GameEngine` 准备新轮时调用 `Globals.Units.StartRound(players, Map.Placed.Values.Any(t => t.Set == TileSet.Core))`。
2. `UnitSupply.StartRound` 只有 `coreRevealed && i%2==0` 才从精英队列取牌；否则取常规。`AppendRegularToOffer` 也只加常规。等再多轮不会在无Core时自然翻出勇者。
3. 当前 `PveBattleController.CreateJourneyMap` 的 authored route **全部建为TileSet.Countryside**。塔/要塞是地点，不会因为征服变成Core。
4. 原地图 `OriginalMapCatalog.Initialize` 也只建立 Countryside；`Assets/Resources/Maps/tutorial_opening.json` 是起始A/乡野1/2，明确不含探索供应或Core。当前原后续板块资料/探索链缺口未关闭。
5. 当前没有合法的既有UI将真实原核心板块翻开并加入map。直接将场景某格改Core、在测试fixture预塞勇者Offer、用HeroBuild已招单位、或免费取得勇者，都不能证明自然精英招募翻倍。

**结论：勇者自然精英招募场景并非单独的价格按钮断路，而是与原核心板块来源/探索同一阻塞。** 当前地图可以自然招常规单位，但不能自然供给勇者。应保留此共同阻塞，先完成真实原核心板块来源及正式探索/跨轮，再以村庄或自有要塞一次实际勇者招募补差额和后续消费者；不为关闭台账制造Core或假初始供给。现有12个招募后台用例和四颜色能力/突袭附费场景都复用。
