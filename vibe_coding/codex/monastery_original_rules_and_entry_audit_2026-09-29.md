# 修道院原规则与当前实际入口审计

仅本地只读核查及本文件记录；未改生产/fixture、未运行CLI/Unity、未联网。结论：**原地点卡/原规则来源齐全，但当前两种Demo地图均无真实修道院，正式连续会话也尚未接入修道院完整生命周期。旧同名类不是完成证据。**

## 已目视的原依据

- `Assets/GameData/cards/place_002.png`：原修道院地点卡。
- `tmp/pdfs/rules_combat_8.png`：中文规则书印刷第8页，交涉及发起焚毁战斗。
- `tmp/pdfs/settlement-10.png`：印刷第10页，胜败结算及烧毁后空格。
- `tmp/pdfs/round_book_page_4.png`：印刷第4页，新轮供应更新。
- `tmp/pdfs/round_book_page_11.png`：印刷第11页，回合末神器奖励/补牌/治疗。
- `tmp/pdfs/round_book_page_6.png`：印刷第6页，交涉、焚毁、休息和移动的行动边界。

以上为实际看图核对。`resources/principle` 中部分旧笔记称烧毁扣1或2声誉、修道院守军为白色等，均与上述原图冲突，不能据此实现。

## 原修道院功能（必须一起保持语义）

1. 未焚毁时为交涉地点：可招原卡含修道院图标部队；**2影响买1治疗点**，按真实手伤/部队等级消费。不是6影响全体治疗，不是直接清空创伤。
2. 揭示每座修道院时，从实际高级行动备用牌库顶抽1张，放到**部队供应区中的独立高级行动列**。它不属于常规三张高级行动供应，更不是英雄技能供应。
3. 在任意未焚毁修道院，以每张**6影响**购买该独立列的实际高级行动，入自己的功能牌库顶；可以同次交涉多买，声誉修正只在交涉开始一次。取得后本轮不立即补位。
4. 新轮准备（原第4页2c）：部队列内剩余高级行动全部归高级行动牌库底；然后每座地图上**尚未焚毁**修道院再加入1张高级行动。普通部队是玩家数+2张，不能把修道院高级行动占成一个部队槽；独立列也不能走常规高级行动`Take`的立即补位。
5. 焚毁是占本回合行动的战斗，开始就扣声誉3，从实际紫色敌堆随机抽1标记；**本场不能使用自己的部队**，不能只禁能力却允许承伤。不是要塞突袭，不因地点自动添加城防。
6. 胜利后才以盾徽标记焚毁，在之后游戏视为**空格**，并在正式回合末获得神器奖励；用已有神器翻数量+1、选择数量、未选归底的正式奖励流程。不能刚点焚毁就标已毁、不能即时给一张随意神器。
7. 无论胜败，抽出的紫标在战后弃掉；失败修道院保留，以后重试抽新标记。不能把`Enemy=null`视为已焚毁。
8. 原规则只明确新轮按未焚毁数量重新发独立列；**未见要求烧毁瞬间删除该列一张已有高级行动**。该列对任意修道院共享，不应无依据把牌强绑定为某座修道院的即时删除物。

## 当前地图与入口：确实缺失

- `resources/original_maps/tutorial_opening.json` 的A/1/2共21原格，地点仅Portal、Village、MagicalGlade、CrystalMine、RampagingOrcs；没有Monastery。`Assets/Logic/Runtime/Map/OriginalMapCatalog.cs` 白名单也只接上述地点，不能自行把其中一格改成修道院。
- 既有 `Assets/GameData/BattleDemo/JourneyRoute.asset` authored路线有营地、森林、边界、沙漠、湖、山、村庄、塔、要塞、地下城；无修道院。
- `PveBattleController.JourneyMap.cs/CreateJourneyMap` 非原地图仅创建fortifiedSite、dungeon或Village，单把某格recruitAt改成Monastery目前也不会生成真实PlaceState。
- `PveJourneySession.SiteInteraction.cs/SiteLocation` 仅村庄、本人要塞、已征服塔/城市，明确注释修道院需完整焚毁生命周期。当前`CanInteractAtCurrentSite`不会开放修道院。
- 因此不能通过改一个招募图标按钮就宣称真实修道院可玩；既没有现成真实地点，也没有完整烧毁状态。原后续板块来源缺口仍独立存在，不新增假原地图。

## 已存在但不可直接信任的旧代码

- `GlobalResources.MonasteryOffer` 已有独立`List<AdvActionCard>`，可以复用其牌区语义。`PlaceManager.Flip(Monastery)`会从`AdvActions.TryDraw`取1张加入它，这一概念正确；需接真实地图揭示且防止同一地点重复发牌。
- `GameEngine.PrepareNewRound`当前没有MonasteryOffer回底/按未毁数量补充逻辑。全源码引用只看到旧Flip和移除扩展，没有正式轮次维护。
- `AdvActionSupply.Take`会立即`FillVacancies`，不能当作修道院购买。需复用同一真实备用牌库但单独移除MonasteryOffer并置顶玩家牌库，次轮统一更新。
- `Interactions/MonasteryInteraction`的“6影响深度治疗”、`unit.Heal(100)`、假定选首张技能，以及`GetAvailableSkills`返回空列表，均不符原规则；影响力扣除还为TODO。不能接到当前正式UI充数。
- `PlaceManager.BurnMonastery(player,bool win)`仅扣声誉并按bool直接抽神器，无地点、实际紫标战斗、参与限制、持久焚毁、回合末奖励。不可把这个bool函数当完成实现。
- 当前`PlaceState`没有独立焚毁标记；`Conquered`对多数地点由无敌人推断，因此正常无驻军修道院也会Conquered=True。烧毁必须有独立事实，不能复用这个值。

## 烧毁后的实际消费者依赖

- **移动费**：`MovementService.GetCost`来自真实地形；烧毁应只取消地点语义，保留原地形/费用/坐标。不能改成免费格或默认平原。
- **安全落点/飞行/地道/传送**：`CardTravelActions.IsSafeDestination`主要检查城防、肆虐和其他玩家占据。修道院不是城防地点，烧毁后也不能凭OwnerId制造敌方要塞/PVP门禁；仍检查地形和其他玩家。相关飞行、德鲁伊、地道、安全末格共享此入口。
- **手牌加成**：原第11页只有本人要塞数、城市盾徽/领袖等加成；`FortifiedSiteActions.HandBonus`目前也只计Keep/City。烧毁修道院放盾徽只是焚毁标记，不是自有要塞，不能增加手限或扩张要塞数。
- **地图及UI**：烧毁后原地点招募、治疗、购买、重复烧毁入口全部关闭；原地点展示应注明已毁/空格，底层地形保留。旧静态地点标签/状态报告不能继续显示为可交涉修道院。
- **本回合**：焚毁开始占行动、声誉−3、实际紫标结算；不得移动/再交涉，部队参与限制应在真实`UnitParticipation`及承伤路径共同生效，战后正常清理不能永久禁部队。
- **供应和实例**：购买/战斗/轮次全程保留原对象与已有牌区；神器奖励需在正式回合末领取；失败紫标弃置但修道院未毁，下一次用同一敌供应抽新实体。

## 可判定的实施与验收缺口

尚缺：真实地图修道院入口；持久焚毁状态；正式2影响治疗/6影响独立列购牌；揭示和新轮独立列生命周期；真实紫标焚毁战斗、部队禁用/承伤限制和战后弃置；回合末神器奖励与烧毁后空格消费者。原资料足够定义这些主要规则，但当前不能生成“已存在修道院自然交涉”场景而不改变地图/生产。此审计不声称后台或场景通过，也没有修改地图以绕过缺口。
