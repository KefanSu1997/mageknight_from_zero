# 法术宝典单层基本输入差距（只读审计，2026-09-29）

结论：不是重做宝典效果；`SpellTomeEffect` 已正确执行实际供应原法术、同色真实弃牌、普通/焚毁及免印刷费（保留额外费）。缺口是 Journey / UI 没接通已有法术输入适配，以及少数共用输入仍无多人/多部队选择。

## 已有与边界
- `Assets/Logic/Runtime/Battles/PveJourneySession.MaterialArtifacts.cs:42-95`：TomeChoices 已按普通/焚毁及阶段筛选；PlayMaterialArtifact 已有单敌 target、一个自有 unit、path、TomeSpellOption。但未调用 PveDeedInputs；UI 只画原分支、单敌和单部队，path 按钮只给德鲁伊法杖。
- `Assets/Logic/Runtime/CardEffects/SpellTomeEffect.cs:17-55`：真实支付牌/供应/同色验证，献祭禁止重复使用宝典支付牌；`ActionSystem.cs:318-358` PlayFromTome 免除原印刷色及黑费、原 effect validator 与额外费用先于弃牌回调。
- 普通固定攻防、单敌减甲/冰冻/消灭、明确 Choice.Option 的分支已能走现有入口。不要统记所有法术未接，也不要把现有选项0合法但不可改选记为效果不存在。

## 实际基本输入缺口
| 原法术 | 当前限制 | 必要输入/消费者 |
|---|---|---|
| magic_002 神游（焚毁） | 无法选 CardsFromDiscard；弃牌堆非空通常会校验失败 | 选择实际两张弃牌（不足取全部），置顶/底原 choice 已有；随后真实抽牌 |
| magic_004 地道穿行/地道攻击 | API有path但宝典UI无路径；未走 ValidateWorldSpell/AttachTunnelBattle | 实际连续地图路径、真正地点守军前校验；强效成功后绑定同Context的正式Battle并接地点奖励 |
| magic_005 重生（焚毁） | 仅一个 unit，原规则允许总等级内多个 | `ReadyUnitTargets` 多选真实自有部队，原 RestoreEffect 按实际森林5/其它3总等级校验；不是改成任意数量 |
| magic_006 辐射（焚毁） | 只能 choice默认0=红；无颜色选取 | Colors一项基本色，原库存伤牌与两晶消费者 |
| magic_011 献祭/牺牲 | 普通只能空材料；强效默认option0不含完整合法配对 | 普通真实至多三材料及每张神器独立晶色；支付弃牌和宝典本体不得再被献祭；强效 BonusChoices 两项（绿/白、红/蓝），复用库存配对贡献 |
| magic_012 宣夺/诅咒 | 立即/延期已有，但低位颜色固定红 | Colors一项实际源中基本色，保留 mode高位；真实ClaimForRound及延期正式回合消费 |
| magic_018 读取/窃取 | 单人也只能默认红；多人缺每位参与者输入 | 单人先复用Colors；多人 `MindReadChoices` 每位真实玩家选择及可选 `MindStealTarget` 目前连PveDeedSelection/现有UI也未覆盖，须明确保留缺口或补最小共享字段，不假定输入适配已有 |
| magic_020 荣誉号召（焚毁） | 默认供应index0且无可选解散，不能选其它真实供应 | OfferedUnit 实际引用、DisbandUnit 可选自有；复用原槽位/单位获得；普通战斗号召本来只授许可，后续已有UseOfferedUnit不必改 |
| magic_023 清风/黑夜之翼 | 宝典UI无path或EnemyIndices；choice.Option固定0未按路径/目标数打包 | 普通实际路径+每格真实移动费；焚毁实际存活敌人多选+原额外移动消费，不能直接取默认0 |

`EnemyAttack` 目前不是上述法术choice必要输入，不因通用结构存在而扩矩阵；应保留未来可兼容attackIndex接口但无需为不存在的法术分支编故事。

## 最小共用接法
1. PlayMaterialArtifact 尾部追加可空 PveDeedSelection inputs（旧签名调用兼容）；仅items_023分支走 PveDeedInputs.Execute。以实际供应法术ID/Spell类型的临时描述对象选择原输入schema；它不进任何牌区、不算拥有、物理SourceCard仍是宝典/实际盒（这种schema descriptor已有PlayOfferedSpell先例）。
2. 回调收到真实packed option再写Context.TomeSpellOption，再执行原ExecuteArtifact；绝不转用普通Play或PlayFromOffer来代替宝典（会改变费用或许可）。外层CardToDiscard保持宝典同色费用，PveDeedInputs的CardsToDiscard仅是献祭材料；复用原冲突校验并补物理宝典排除。
3. 复用 `PveJourneySession.OfferedSpells.cs:16-63` 的真实目标/自有单位/上下文finally模式、ValidateWorldSpell和AttachTunnelBattle；不复制法术规则。PveDeedInputs本身已经scopes path、目标、选色、材料、供应解散并复原。
4. `magic_005`多单位目前PveDeedSelection只有Unit，应最小增加Units或独立尾部targets，复用`PveJourneySession.cs:162-191`现有重复/所有权验证，保持旧单选兼容。
5. UI从宝典选供给和付费材料后复用DeedInputs面板（显示所借原法术/强弱），保留外层宝典实体/付费牌/实际供给引用；不能选择另一次神器/复制来扩大层级。
6. 测试仅新增输入桥：神游实际弃牌；献祭支付与材料排除/强效选择；颜色+真实源/库存；荣誉号召非首供应/真实槽；路径/目标+正式世界消费者；多单位。每组失败断言材料/宝典/法术供应/资源均未变，成功供应不移出且物理来源保持。免色黑费、额外费历史已有，无须再穷举四颜色全法术。

## 可复用证据（不是本次新通过）
- `Assets/Tests/RulesState/SpellTomeStateTests.cs`：同色原牌实际弃置、法术保留供给、白天焚毁免印刷费、额外红费仍支付、失败守恒、权限不泄露。
- `PveMaterialArtifactEntryTests.cs:67-80` 已含普通/焚毁magic009真实战斗和magic007错误阶段拒绝与正式近战目标；`pve_material_artifacts_acceptance_index.json` 61步98断言（场景有效前段20260923_065024_a1e46fb0加后段20260923_065159_d7b49c28）实际宝典焚毁借烈焰浪涌→原火攻击败；这只证明已选分支。
- `pve_deed_inputs_acceptance_index.json`：神游实际弃牌回收/正式补牌。
- `pve_shield_summons_acceptance_index.json`：献祭实际材料独立晶色、牺牲四色配对、荣誉号召真实供给/解散/槽位、普通战斗号召真实消费者。
- `pve_mana_spells_acceptance_index.json`：006/012/018原合法单人选择及实际消费者；scope明确solo，不代表多人读取已接UI。
- `pve_world_spells_acceptance_index.json`：004实际同守军/地点奖励，023实际路径/移动费和存活敌目标。新增只验宝典桥接与去向，不重跑其整套原规则。

本次仅阅读源码/历史索引并写此报告，无生产修改，无CLI/Unity/网络操作。


13:42增量：宝典已接PveDeedInputs现有schema及世界战斗绑定，后台新增5+回归4有效；蓝宣夺及荣誉非首单位连续场景82步153有效断言、4图和Console双0。见 `worklog_2026-09-29_tome_dynamic_inputs.md`。神游/献祭/世界路径新桥场景仍待补；重生多单位、读取多人、荣誉供应超3分页仍缺，不能将代表性通过泛化全部模式。


13:58增量：宝典献祭独立材料/实际晶体消费、神游回收抽回再消费85操作151有效断言；飞翼实际移动费用→正式跨轮地道同守军征服与奖励69操作131断言一次通过。共154步282断言、8关键图、Console双0，后台复用不重跑，无生产修改；见 worklog_2026-09-29_tome_material_world_scenes.md。材料/世界路径桥不再待场景；重生多部队、读取多人、荣誉供应>3分页及其它明确缺口仍待。


14:13增量：共用重生Units原多选及荣誉3张分页已接，后台6新增+1直接回归有效通过。合法两构筑场景共134操作200有效断言、7图、Console双0：第四供应赤袍取得后攻击；两真实单位重生恢复后正式下一回合农民移动、同僧侣攻击，英雄伤1保留，五处实例一致。见worklog_2026-09-29_restore_honor_ui.md和pve_restore_honor_inputs_acceptance_index.json。此前多部队/供应>3缺口已补，森林5/拒绝细项仅后台，不泛化全模式；多人读取/窃取尚未接，下一步依据multiplayer_mind_inputs_audit_2026-09-29.md。


14:31增量：原多人读取/窃取实际响应入口完成，后台新增11+直接回归1一次通过；双英雄两条正式跨轮路线227有效操作357断言、8关键图已查看，Console双0。实际蓝响应弃牌获晶并支付移动、绿色原行动同实例永久偷取后强化移动；无白色展示完整真实手牌、明确不偷取且双方同名牌归属独立。见worklog_2026-09-29_mind_response_ui.md与pve_mind_responses_acceptance_index.json。共用018输入缺口退出队列；宝典多人借用未单独场景，不泛化全复制矩阵。无夹具临时修改、运行测试/代理或push，目标active。下一步有限基本差集核实，重点头环翻面/持久技能的合法借用；原后续地图来源仍缺，不重复已通过范围。
