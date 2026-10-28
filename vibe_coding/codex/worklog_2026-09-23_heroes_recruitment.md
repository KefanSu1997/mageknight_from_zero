# 勇者交涉招募：声誉翻倍与暴徒互斥（源码完成，待验证）

## 规则缺口及修正

核对[原Lost Legion扩展规则第5页](https://www.mageknight.net/wp-content/uploads/Mage-Knight-Board-Game-Expansion-The-Lost-Legion-Rules-2013.pdf)时发现，前一批四勇者源码未覆盖招募费用旁的声誉翻倍标记。该规则作用于整次交涉，正负修正均翻倍，多支勇者只应用一次，并禁止同次交涉招募暴徒；荣誉号召、指挥旌旗等非交涉取得不触发。

- `army.json`四原勇者增加`double_recruit_reputation`明确元数据，`UnitJsonLoader`读入`UnitCard.DoubleRecruitReputation`。不从能力声誉+1或部队名称推测招募规则。
- 正式`RecruitmentService`沿用交涉开始时冻结的修正，在首支勇者成功招募时对整次交涉补差。`ActionContext.InteractionDoubled`记录已经应用，普通部队/后续勇者共享，不随着当前声誉变化重算；正式回合清理删除该状态。
- 招募前校验勇者与暴徒互斥；失败不修改实际玩家、费用、供应或交涉状态。独立的`RecruitFree`保持不触发翻倍。
- 本次不修改演练层旧`applyReputationModifier:false`入口；完整连续Demo必须走正式交涉上下文，不能以这些旧入口证明完整招募规则。

## 定向验证准备

新增`HeroesRecruitmentTests`12个参数展开案例，加`FreeRecruitStateTests.FreeHeroesDoNotDoubleOngoingInteraction`2例；四原定义沿用前批17例中的定义检查增加标记断言。没有运行测试，未增加后台/场景通过数。

新案例验证：两支勇者只翻倍一次，正/负声誉；勇者前后购买普通原农民总价一致；实际影响5能力改变声誉不重定价；两个方向勇者/暴徒互斥；缺费用、缺槽、地点错误保留状态；回合结束清理后可招募暴徒；真实原法术/神器免费取得保留原费用、去向且不触发翻倍。

资源允许后与此前待验范围合并，只编译一次。旧共享交涉回归选实际受影响的9个暴徒案例和2个免费暴徒案例，不重跑声誉表14个纯映射例或全卡库。

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$roundDeckFilters = Get-Content vibe_coding/codex/round_deck_lifecycle_filters_2026-09-23.txt
$recruitFilters = @(
  'MK.Tests.Cards.HeroesRecruitmentTests',
  'MK.Tests.Cards.FreeRecruitStateTests.FreeHeroesDoNotDoubleOngoingInteraction',
  'MK.Tests.Cards.FreeRecruitStateTests.FreeThugsDoNotReverseOngoingInteractionAndRecruitRewardsStillApply',
  'MK.Tests.Cards.ThugsRecruitmentTests.TwoThugsReverseOnceAndEndTurnClearsState',
  'MK.Tests.Cards.ThugsRecruitmentTests.MixedPurchasesHaveTheSameWholeInteractionTotal',
  'MK.Tests.Cards.ThugsRecruitmentTests.ActualThugsAbilityChangesReputationButNotFrozenModifier',
  'MK.Tests.Cards.ThugsRecruitmentTests.FailedRecruitCannotReverseOrSpendAnything',
  'MK.Tests.Cards.ThugsRecruitmentTests.NormalBonusAndCharmDiscountAreEachConsumedOnlyOnce'
)
dotnet run --project tools/RulesCli -- . @roundDeckFilters MK.Tests.Cards.RoundFlowBoundaryTests MK.Tests.Cards.SpaceBendingBasicStateTests.OrdinaryRunOneTurnRetainsNormalRoundCountdown MK.Tests.Cards.ForestersMovementStateTests MK.Tests.Cards.BasicTerrainMovementStateTests MK.Tests.Battles.PveIllusionistSorcererTests MK.Tests.Battles.PveHeroesUnitTests @recruitFilters
```

02:03物理可用约0.81GiB，虚拟余量约1.49GiB；用户02:20前不用Unity的约束有效，未编译或启动编辑器。差异格式和原JSON检查不能替代执行。待后台通过后，连续场景还需真实交涉购买、显示价格和剩余资源，再取得Console两次零错误；原19/30部队场景基线不变。
