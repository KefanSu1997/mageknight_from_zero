# 修道院真实生命周期：后续世界扩展设计审计

2026-09-29。仅审计原来源与现有后台，不修改生产、不运行 CLI/Unity、不新增场景批次。**完整修道院不是当前原战斗 Demo 初版的明确前置**；三类僧侣已有合法构筑/攻防消费者，不能把地点世界功能缺口混为部队未接入。本文保存后续扩展方案，不声明规则已实现或通过。

## 原来源

- `Assets/GameData/cards/place_002.png` 原修道院卡：未焚毁可交涉招募；2 影响购买 1 治疗；6 影响取得修道院特殊高级行动、放功能牌库顶；攻击扣 3 声誉，抽紫色守军，普通部队不能参战；成功焚毁并获神器，胜败均弃守军。
- 已观察本地原规则页面 `tmp/pdfs/rules_combat_8.png`（印刷第 8 页交涉）：修道院特殊高级行动购买后不立即补位，下一轮才补。`tmp/pdfs/settlement-10.png`（第 10 页）：成功焚毁后视为空格，神器在回合末领取。
- 已观察 `tmp/feishu_docs/rulebook-04.jpg`（印刷第 4 页轮次准备）：部队供应区原高级行动**全部**放回高级行动牌堆底，再按已揭示、未焚毁修道院数重新发特殊高级行动，随后才轮换普通高级行动供应。不是仅补特殊供应空缺。
- `resources/text_json/skill.json` 的忠诚契约原文允许该实际绑定单位在通常不能用部队时使用。复用 `UnitParticipation` 的正式例外，不把“禁止普通部队”改为禁用所有单位。
- 前置条件详见 `natural_site_recruitment_audit_2026-09-29.md`；该旧审计中的塔/城市招募与购法术缺口此后已有实现，本文不回退其实际进度。

## 实际现状与不能复用的假路径

`PlaceState` 无焚毁字段；其普通 `Conquered` 只按守军为空判断，正常修道院本来就无持久守军，不能作为焚毁判断。`PlaceManager.BurnMonastery(player,bool win)` 只扣声誉/立即抽单张神器，完全不绑定地点和真实战斗，不得调用。旧 `Interactions/MonasteryInteraction` 亦不是原版治疗/高级行动规则。

`GlobalResources.MonasteryOffer` 已是独立的真实 `List<AdvActionCard>`。`PlaceManager.Flip(Monastery)` 会从同 `AdvActions` 底层牌堆抽一张加入；但当前原地图直接创建 `PlaceState`，未接该 Flip，且该列表无正式跨轮轮换。`GlobalResourcesExtensions.RemoveAdvActionFromSupply` 仅 Remove，不能提供合法交涉、精确引用、付款和取得流程。

`DungeonActions` 已有真实供应抽敌、胜败回收、回合末神器翻二选一、正式领取；可复用规则部件，但修道院不能假装 Dungeon：不强制夜间、不随机掷骰决定法术/神器、成功后不能再次袭击、没有地下城永久奖励状态。

## 最小共用方案

### 真实地点及战斗

1. `PlaceState` 增持久 `MonasteryBurned`（建议受控写入）。保留同一地点引用、原 Type、原卡与 OwnerId，成功后交涉/探索地点行为视为空格；逐一检查读取 Type 的消费者，不能只隐藏 UI 按钮。是否以专用有效地点查询集中处理，按实际消费者决定，不全库重构。
2. 新 `PveJourneySession.Monastery` partial 提供 `AttackMonastery`。从当前 `Map.Placed`/`Places[Player.Position]` 取真实未焚毁地点；复用正式战外、待选效果完成、未休息/未行动门槛，校验同一真实 EnemySupply 且紫堆可抽。全部可预检条件成功后，抽真实紫敌、扣 3 声誉、提交本回合行动，建立同 Player/Context 的 PveBattleSession。不要对 UI 接收 bool 胜负，不生成替代敌人，不凭牌面数据重建敌实例。
3. 用专用 `MonasteryActions` 保存该次真实地点/敌引用及完成状态。`UnitsAllowed=false` 保留忠诚绑定例外；不改 DayPart、不额外赋予地点城防。正式 `UnitDamageResolver.EndBattle` 在退出战斗后调用 Complete，校验同上下文、同敌人，用真实 DefeatedIndices 判胜负。
4. 胜败均把同紫敌送回 EnemyTokenSupply.Discard（保留傀儡实际保留机制），不可重复回收。失败不置焚毁，下次必须重新抽敌；成功置同 PlaceState 焚毁/盾徽归属并登记一个神器奖励。恢复战前部队许可，不污染下战。`PveJourneySession.CompleteTurn` 排除该已回收随机遭遇敌，不永久加入其 `_defeated` 集合。

### 正式回合末神器

最小提取 `DungeonActions` 中真实神器 DrawTwo→引用选择→Validate→KeepOneReturn→牌库顶的共同部件，地下城仍保留自身奖励类型/掷骰等行为，修道院固定一个神器奖励。避免复制两套抽牌/归底规则，也不引入通用奖励框架。

在 `PveJourneySession.EndTurn` 与 `PlayerTurnEngine.EndTurn` 原 Dungeon 门禁旁校验修道院待选奖励，在正式 DrawAfterEndTurn 之前领取。未选不能先结束回合/弃牌后再失败；真正取得原神器可被本次回合末补牌抽到，并在后续真实回合消费。反复点击选择/结束不能重复取得。

### 交涉及供应

- 扩展已有 `SiteLocation`：仅 `Type==Monastery && !MonasteryBurned`，沿用 `BeginSiteInteraction`、原声誉门槛、绑定地点/坐标、`RequireSiteInteraction`、`RecruitSiteUnit`。保持村庄 wrappers 严格村庄。普通招募继续实际 Monastery 图标、原费用、槽位、魔宠等既有约束。
- 增实际地点治疗购买入口，根据已验证地点使用村庄 3/修道院 2 的原费用；获得共享 Healing 后仍用正式伤牌/部队治疗消费者，不另造“全治”规则。
- `BuyMonasteryAdvancedAction(AdvActionCard offered)`：先验证正式当前修道院交涉、同地点引用、真实 `Globals.MonasteryOffer` ReferenceEquals、6 影响；付款成功仅移除所选实体，原 ID 转 DeedCard 放牌库顶。**不能调用普通 AdvActionSupply.Take**，它会立即补位。一个修道院可以买特殊公共供应中任意一张，不按“此院专属牌”绑定。
- 揭示每座实际修道院时，从同 AdvActions 底层牌堆追加一次真实特殊牌。该初始化必须在 `InitializeDeedSupplies` 建好真实牌堆后，且按地点引用幂等；不能每次访问/开始交涉再抽一张。`MapState.RevealedSites` 当前表示守军可见，不宜混用成特殊供给初始化标记。
- 下轮：全体旧 MonasteryOffer 归同高级行动牌堆底，清空，再按实际已揭示未焚毁地点数量抽新牌；随后普通高级行动供应轮换。`AdvActionSupply` 目前只有 Draw/TryDraw，需最小真实归底入口。`GameEngine.EndRound` 当前先调用 Globals.EndRound（普通高级行动轮换），而单位轮换在 PrepareNewRound，因此必须有明确一次性顺序接入，不能两处各补一次。
- 原来源没有要求焚毁时立即丢弃公共特殊牌；按已核规则，供应规模在下轮按未焚毁数重算，不擅自删除一张无法归属到该院的公共牌。

## 耦合文件与边界

必需：`PlaceState.cs`、新 `MonasteryActions.cs`/`Battles/PveJourneySession.Monastery.cs`、`ActionContext.cs`、`UnitDamageResolver.cs`、`PlayerTurnEngine.cs`、`Battles/PveJourneySession.cs`、`Battles/PveJourneySession.SiteInteraction.cs`、共享神器奖励部件及 `DungeonActions.cs`、`GlobalResources.cs`、`AdvActionSupply.cs`、`GameEngine.cs`。

地图揭示接点另核：`PlaceManager.Flip`、`Map/OriginalMapCatalog.cs` 及真正原地图探索入口。当前 `Map/ExplorationService.ProcessTileEvents` 仍有按随机概率生成元素的简化旧流程，不能直接当原地图修道院揭示来源；只在真实原板块来源落地后接一次性供给注册。当前起始原七格无修道院，不能把后台预置地点测试记为完整原版探索通过。

`UnitParticipation`、原 Healing 消费者及 EnemyTokenSupply 保持共享规则，不为修道院复制。`PlaceManager.BurnMonastery(bool)` 与旧交涉框架不接入新入口；是否后续移除另立需求，不在本轮顺带清理。

## 后续实施时的有限验证

- 一条真实连续后台用例：未烧交涉真实招募/2 影响治疗实际目标/6 影响取得特殊原卡→正式回合末抽到并消费→下回合实际攻击扣 3→同紫敌实际战斗→成功焚毁→正式神器翻二选一/归另一张/回合末取得→后续回合使用同神器；普通单位禁用且忠诚原例外合法。
- 一组失败与持久性：无紫敌、已行动、位置漂移、同 ID 非供应引用在付款前拒绝；失败战斗仍扣声誉但不焚毁且回收真实敌，下次抽取；结束重入不重复奖励；焚毁后不再招募/治疗/购牌/袭击。
- 一组实际跨轮供给：两个院共享两个特殊原牌、买一张不立即补，焚毁一院后现供应不造额外变化；正式跨轮全部旧牌归底，按剩余院数抽新牌，且早于普通高级供应轮换，重复准备不再抽。重复揭示注册不重复追加。
- 抽取共用奖励部件后只补直接受影响的原 Dungeon 神器选择/正式回合末消费者。不要重跑整卡库。后台通过后再并入后续世界扩展连续路线，不额外生成当前场景验收批次。

状态：**设计审计完成；上述完整修道院功能尚未接入、未后台验证、未场景验证。** 当前主线继续原探索与原卡/神器证据闭环，本方案不扩大本轮任务。
