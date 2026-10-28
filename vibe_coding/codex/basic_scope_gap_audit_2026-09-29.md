# 原版基本范围差距审计（2026-09-29，最后共享场景之后）

只读复核现有代码与索引；未运行测试、Unity、网络或生成器，未改生产。此文是下一步队列，不新增通过数。路径均相对仓库。

09:30增量状态：下文是早前审计快照，已完成范围须读取新证据覆盖。地下城神器奖励已有 `pve_dungeon_artifact_acceptance_index.json`；动态头环已有 `pve_headband_dynamic_acceptance_index.json`、`pve_headband_targets_acceptance_index.json`、`pve_headband_rally_heal_acceptance_index.json`、`pve_headband_pain_acceptance_index.json`、`pve_headband_leadership_acceptance_index.json`。原伤牌已从本地原PDF提取并实景验证，不能再列为缺素材。其余未被这些明确覆盖的模式、原英雄图及原板块探索仍保留。

10:00增量：原七英雄身份图已由实际Player.HeroId绑定原专属牌插画，见pve_original_hero_portrait_acceptance_index.json；头环Shape/Fork/Flight见pve_headband_shape_fork_flight_acceptance_index.json。余下头环基本缺口具体清单见tmp/headband_remaining_basic_audit_20260929.md。原地图和未覆盖规则仍保留。

## 基线与判定

12:35增量纠正（保留下文原审计，不直接覆盖历史失败）：原A/1/2三板块21格/18陆地、原肆虐敌人、位置导航、原林地/矿坑回合奖励及魔宠林地招募/跨轮维持已接入并场景通过，分别见当日 `original_map_runtime`、`original_sites_runtime`、`original_navigation`、`glade_recruitment` worklog。后续原乡村/核心板块供应与完整探索仍缺真实来源，不能把原起始图示冒充完整地图。原英雄头像及伤牌原图已补，不再作为未修缺口。德鲁伊变换的地形输入本轮已接、后台22有效通过，场景第26步UI截断已修，前缀29+后缀85=114有效断言通过，见worklog_2026-09-29_shapeshift_druid_scene.md。头环社会/实际普通付款/部队付款已由当前goal的10:15–11:15记录覆盖，仍保留未覆盖的翻面/决斗/傀儡等具体范围。

- `basic_card_acceptance_index.json`（同目录）保留 2026-09-18 的 **121/121 有效原行动/高级行动/法术/神器有限基本场景证据**。不能因连续 Demo 后加入口便重跑全库，也不能把该历史基线当成当前所有动态操作均已可玩。
- `worklog_2026-09-29_shared_final_scenes.md` 与三个 `pve_shared_*_scene_index.json` 补齐七共享中的最后六项，当前 **70/70 是自身技能合法入口及所列主消费者场景证据**，不是所有基本模式已完成。
- 部队与神器分散在 `pve_*_acceptance_index.json`，旧索引的“19/30”“其余53技能”等分母/缺口已被后批部分覆盖，不能直接相加或照搬。`pve_outside_units_acceptance_index.json`、`pve_headband_progression_acceptance_index.json` 等新证据应覆盖旧状态；本次不臆造一个新的 30/30 或 25/25 场景完成数。

## 按优先级的具体下一步

### 1. 头环普通借用的动态技能合法输入与效果隔离（真实接入缺口）

代码核实：`Assets/Logic/Runtime/Battles/PveJourneySession.SpecialArtifacts.cs` 的 `HeadbandChoices` 仍只取静态 `BattleSkillChoice.For`；`PlaySpecialArtifact` 只有 `skillOption`、单敌/单位等有限输入，没有实际手牌/伤牌/贡献/持久翻面等专用选择。`Assets/Logic/Runtime/CardEffects/HeadbandEffect.cs` 明确拒绝混乱大师普通借用；普通执行直接调用公共技能的 Effect 两次/一次，而新技能常委托要求真实 owner 的 Actions。因此取得后自有技能可用，不证明普通临时借用可用。

先列出实际公共供应中允许头环普通借用的技能与所需输入，优先补最基本的动态选择与持久标记隔离；不把公共技能伪装成自有，也不放开互动技能禁令。用同一局实际公共供应验证一项动态普通借用及后续消费者；既有普通静态借用/焚毁取得证据保留。证据：`pve_headband_progression_acceptance_index.json`、`pve_social_persistent_acceptance_index.json`、`pve_targeted_combat_skills_acceptance_index.json`。混乱大师独立盾状态需要真实设计，不可仅删拒绝。

### 2. 地下城神器奖励正式选择、入手与后续出牌（已接但缺场景）

`Assets/Scripts/Battle/PveBattleController.Dungeon.cs` 已有 `DrawDungeonArtifactReward` 两张实际奖励按钮；`PveBattleController.cs` 回合末调用 `RevealDungeonArtifactReward`，不是“尚无UI”。当前场景只走黑骰法术奖励。复用已有地下城连续路线，选择可重放的神器奖励结果，验证翻二选一、未选牌去向、正式回合末领取及取得后实际使用；不重跑忠诚供应/招募/格挡全部已过段。依据 `pve_loyalty_dungeon_acceptance_index.json`、`worklog_2026-09-29_loyalty_dungeon.md`。完整棕敌目录是独立素材/数据缺口，不应伪称已验证全部敌人。

### 3. 合并补原卡主要模式的连续场景证据（先查历史，避免重测）

以下索引明确保留本 Demo 缺失模式，但 **不等于原卡历史基本验收失败**。先对照 121 张台账中的对应模式与消费者；只有历史未覆盖或当前新输入/生命周期受改动的才补：

| 范围 | 明确缺口 | 证据索引 |
|---|---|---|
| 学习/修行/法术熔炉 | 学习强化、修行基础、熔炉强化；学习取得牌后来实际抽出并使用 | `pve_supply_deeds_acceptance_index.json` |
| 魔力风暴/神游/鲜血仪式 | 风暴基础、神游指定两张、鲜血仪式强化 | `pve_deed_inputs_acceptance_index.json` |
| 魔咒/两极分化 | 神器与内部后付款卡尚不支持本次技能输入；源骰/强法术/部队主要付款场景未覆盖 | `pve_payment_skills_acceptance_index.json` |
| 变换身形 | 德鲁伊额外地图格/地形输入没有由 `PlayShapeshift` 传入；其他固定项不能由三个例子推全覆盖 | `pve_shape_repel_acceptance_index.json`、`Assets/Logic/Runtime/Battles/PveJourneySession.cs` 的 `PlayShapeshift` |
| 傀儡/决斗 | 傀儡格挡是不同主要能力消费者；决斗部队影响失奖是印刷奖励条件 | `pve_targeted_combat_skills_acceptance_index.json` |

优先把同英雄、相容构筑、实际回合可完成的几个模式并进连续路线。无需逐颜色穷举、三目标所有组合、所有随机骰结果或全生命周期每个排列。

### 4. 原板块探索与必要地图消费者（真实可玩缺口）

`pve_map_skills_acceptance_index.json` 明确原七格板块定义/正式探索尚未接入当前 Demo，夜间鹰眼减费与一格飞行只有后台。地图是自定已揭示格，不能拿移动到一格代替探索原板块。先接真实板块供应/合法位置与正式探索服务，再合并野性盟友/鹰眼/斥候的实际探索费用和可见信息验证；保留已通过移动/湖山付款/白昼两格同守军证据。

`Assets/Logic/Runtime/Battles/PveJourneySession.World.cs` 已有地道真实地点突袭，不能把旧索引的“未接普通地点突袭”泛化为所有地点没有战斗。普通地点/城市完整入口需单独核具体调用者，不扩展成此次 PVP 工作（该代码明确拒绝玩家战斗）。

### 5. 原素材与重复利用（已见实际显示缺陷）

最后共享场景人工检查已明确：伤牌无原图、所有英雄共用剑士立绘、地图自定共用背景，见 `worklog_2026-09-29_shared_final_scenes.md`。优先复用现有原伤牌图片与按英雄身份绑定已有素材，缺素材明确列单；不生成自制卡面冒充原图。仅针对修正的代表视图留截图，不重跑全部规则场景。

## 保留但不立即扩大本阶段的组合范围

- 宝盒借恐惧旌旗多目标焚毁与武器专用选择，`PveJourneySession.SpecialArtifacts.cs` 有明确拒绝：这是宝盒揭示这些结果时的真实输入缺口，应登记后集中补专用输入；不因此重测25种神器所有交叉组合。
- `PveCopyInputs.cs` 的多层复制拒绝属于用户已暂停的边缘组合，不作为当前基本阶段阻挡项。
- 多 token 傀儡库存、叉形三目标/抗性全部排列、各种金色转换与神器叠加、每张部队重复招募/换轮穷举均不新增默认队列。已有后台拒绝与守恒证据继续保留。

建议下一批先完成第1/2项和第5项原伤牌显示，再用按卡/模式的最小差集安排第3项；第4项需要真实地图资产与实体接入，单独作为成品可复用工作推进。每批继续分别记“已接入”“后台通过”“场景通过”。


14:46增量：自有决斗部队参与失奖与傀儡库存实际格挡两合法构筑场景完成，共142有效操作189断言、6关键图已看、Console双0；后台定向XML复核复用未重跑。农民贡献正式防御结算后取消决斗奖励；同一玩家保存哨卫跨回合转物理格挡2+真实蓝费决心5挡迅捷6，旧伤2保留并原牌击杀。见pve_persistent_primary_acceptance_index.json。无生产规则/fixture改动，duel第40步时点失败保留，仅后段补验。头环特殊借用仍未解决，headband_remaining_basic_scope_2026-09-29.md区分原发行复合规则与社区线索；狂乱强化部分提交风险、决斗同敌复合、傀儡本回合库存限制待处理。无运行测试/代理，无push，目标active。
