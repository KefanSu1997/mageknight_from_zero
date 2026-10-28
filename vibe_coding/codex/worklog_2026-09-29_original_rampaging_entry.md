# 原地图肆虐旅程窄接（待主代理验证）

目标：原兽人/龙地点使用实际供给标记进入同玩家旅程战斗，防止直接踏入有敌格及二次初始化战斗。

- MovementService.TryMove在移动/附加蓝费前拒绝有敌兽人或龙格。
- RampagingEncounter.Challenge校验相邻、原地点类型、实际在用供给标记及行动时机；不移动模型。
- 新PveJourneySession.Rampaging.cs接主动挑战和移动引发的强制战斗；沿用实际ActionContext、敌实例及已启动TurnMachine/GameState。
- PveBattleSession可接管真实RampagingEncounter，校验玩家/上下文/敌身份，不再次BeginBattle。已有结算继续复用UnitDamageResolver.EndBattle、供给回收和PlaceState声誉奖励。
- 新fixture MK.Tests.Battles.OriginalRampagingJourneyTests，共5参数化用例：主动/强制真实杀敌与正式回合、兽人/龙踏入拒绝、非相邻及外来标记拒绝。

仅完成源码与测试编写、静态接口核对。按分工未运行CLI、Unity或测试；后台通过与场景通过均待主代理记录，不宣称通过。未改地图目录/原数据或UI。
