# 连续流程前置：正式轮次边界（待验证）

本轮继续轻量开发，01:21可用物理内存约1.20GiB、虚拟余量约1.30GiB。未启动.NET编译或Unity，02:20前不用Unity的用户约束仍有效。上一轮幻术师/巫师规则与UI接线属于实际进展；本轮继续完善连续游玩必需的正式入口。

## 原规则与实际缺口

重新查看已渲染的本地中文规则书第4页：回合开始时牌库空可声明结束轮次，声明替代自己的整个回合，其他玩家各有最后一回合；完成自己的最后回合后不能再使用效果。第6页补充无手牌且无牌库时必须声明。现有GameEngine允许重复StartNewRound刷新部队/牌库、任意AnnounceRoundEnd重置倒数、尚有活动回合时EndRound。BeginNextTurn还会让应跳过的空牌玩家实际执行StartTurn，可能发放不该获得的每回合资源。

## 修改

- `GameEngine`增加轮次已开始及活动玩家只读状态。StartNewRound重复调用先拒绝，非空战术检查玩家数量、昼夜、1—6顺序编号和不可重复，全部在重掷/重整/补牌前验证。
- `RoundOrderService.PeekNextPlayer`用于查看轮到谁而不提前移动顺序；未建立顺序明确报错。
- `CanAnnounceRoundEnd`只允许下一名实际玩家在未开始回合、牌库空且无人声明时宣布；声明消耗此顺序位置，不执行StartTurn/EndTurn，只为其他玩家保留N-1个最后回合。
- 显式BeginNextTurn在无手牌/无牌库且未启动轮末时拒绝，提示改为声明；便利RunOneTurn调用路径自动执行必需的声明。已结束轮次不得继续开回合。
- 活动回合或其他玩家最后回合尚未完成时不能EndRound，已完成的可玩轮次不能重复EndRound推进昼夜；最后回合的额外回合信号不挤占其他玩家最后回合。
- 保留旧后台夹具的“新建引擎+空战术只刷新状态”调用，明确该入口不能开始可玩回合；已经进入过正式玩家顺序的引擎不能改用空战术绕过下一轮选择。直接构建的低层轮末牌区测试仍能单独调用EndRound。这些兼容调用不作为完整可玩轮次的证据。

## 验证准备

新增`RoundFlowBoundaryTests`11个参数展开案例：声明者跳过并保留原牌、其他玩家最后回合、单人结束、非空牌库拒绝、强制声明、禁止中途/重复刷新、无效战术原子拒绝、状态刷新兼容入口不可游玩、额外回合不得覆盖另一玩家最后回合。战术夹具仅指定顺序，显式不声称验证原战术效果/美术。

修正`SpaceBendingBasicStateTests.StrongUsesEngineForAThenAThenBReturnsOnlyPlayedCardsAndRestoresSourceAtRoundEnd`：原B无手牌无牌库，旧测试让B执行空回合并在A最后回合前强制结束轮次。现在B正式声明，A通过同一引擎完成最后回合，之后才轮末恢复时间扭曲实体。保留原卡、额外回合、移除牌及守恒断言。既有`OrdinaryRunOneTurnRetainsNormalRoundCountdown`也属于直接受影响回归。

当前仅静态/差异空白检查，不是编译或测试通过。资源允许后合并当前待验范围只编译一次：

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$roundDeckFilters = Get-Content vibe_coding/codex/round_deck_lifecycle_filters_2026-09-23.txt
dotnet run --project tools/RulesCli -- . @roundDeckFilters MK.Tests.Cards.RoundFlowBoundaryTests MK.Tests.Cards.SpaceBendingBasicStateTests.OrdinaryRunOneTurnRetainsNormalRoundCountdown MK.Tests.Cards.ForestersMovementStateTests MK.Tests.Cards.BasicTerrainMovementStateTests MK.Tests.Battles.PveIllusionistSorcererTests
```

仍缺原战术完整定义/素材与取得时效果、补牌后实际选择战术的UI、完整连续控制器及原要素全集入口。此批不能替代这些缺口；不增加后台/场景通过数。所有相关后台通过后，才集中验证Unity实际连续流程和Console双0。
