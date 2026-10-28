# 原附旗轮末选择后台

目视原规则书第5页“使用部队”8b（本地tmp/feishu_docs/rulebook-05.jpg）：一轮结束决定保留部队上的旗帜或洗回功能牌库。沿用BannerActions.ChooseRoundReturn与EndRound/Release；指挥旗失去后原部队被移除，既有正式规则不重写。

新增PveJourneySession.Banners.cs提供RoundBannerUnits(owner)、IsBannerReturning(owner,unit)、ChooseBannerRoundReturn(owner,unit,bool)。GameEngine.CanChooseRoundBannerReturns以已开始轮次、无活动回合、尚未准备新轮且全部最后回合已结束判定真实窗口；防止EndRound后Tracker仍为true导致重复选择。所有权以实际Players与Unit引用校验，可选择任一实际玩家而不改最后当前玩家视图。

CLI 20260929_124707_fac1118d：7/7通过，4新增+3直接旧回归。新用例正式附旗/招募→结束回合→宣布轮末→双玩家最后回合→选择/取消→EndRound→PrepareRound，检查普通部队/伤牌/耗竭、原牌一次洗回、指挥部队销毁及双玩家归属。没有伪造旗引用或直接写轮末计数。未操作Unity，UI与最终场景由主代理执行。
