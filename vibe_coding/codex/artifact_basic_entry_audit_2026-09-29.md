# 25种原神器基本入口审计（2026-09-29）

本轮只读原目录、当前源码、已有索引/记录及报告位置；未运行测试、Unity或网络，未更改生产。此表不新增通过数，不把旧索引的 remaining/not_completed 当成当前缺口。

## 当日最终证据映射补充（优先于下文早期缺口快照）

逐 ID/普通/焚毁台账见 `artifact_primary_evidence_ledger_2026-09-29.json`，25 ID、50模式记录，复用旧有限验收与当前 PVE 索引；不是50个新场景通过。旧 partial/失败状态保持，宝盒借用模式按实际 UseMysticBox 操作而非揭示步骤 enhanced 字段辨认。

- 防护旗焚毁 `items_002_01`、蓝/白/绿戒焚毁各 `_04/_06`、智慧书焚毁 `_01/_30`、皓日焚毁 `_01/_08` 已映射原历史消费者，不再作为“找不到另一面证据”排队。德鲁伊法杖四普通选项及焚毁两项复用 `20260917_225117_538fa50d/items` 的 `_02..07`；历史 partial 不改写为全规则通过。
- 金币包焚毁已不只是历史：`pve_natural_site_recruitment_acceptance_index.json` 真实付费招魔像后再战，`pve_tower_spell_purchase_acceptance_index.json` 实际7影响购法术后下一回合击杀；要塞 `20260929_174623_6a3a489e` 61–70步原影响9减声誉1后8、实际付款7、正式跨轮同魔像再消费。
- 黑暗护符焚毁三独立颜色已有当前要塞场景：同报告26–50步黑/黑/红，实际地道绿黑、火球红黑付款，攻城8击败原025；原红晶保留至下一轮消费者。普通亦保留既有证据。
- 下文宝盒输入、附旗轮末、宝典主要输入、头环翻面/决斗/傀儡早期缺口已有当日后续有效索引。**当前明确待接/待验为头环普通借混乱**；不能用自有混乱或焚毁取得混乱代替。宝典多人等共用输入证据不泛化为每项宝典独立场景，宝盒也不扩大成25神器复制矩阵。
- 最新 PVE 索引没有重拍某模式图片，不构成重新测试理由；查看台账所指原 case/报告及原索引截图审阅范围。

## 判定与来源

- 唯一目录 `resources/text_json/items.json`：items_000–024 共25种，原图 `Assets/GameData/cards/items_XXX.png`；卡面普通/焚毁文字对应 JSON 的 assign_effect/once_per_round 字段。
- `basic_card_acceptance_index.json` 的历史121/121有限基本验收包含全部25种神器，逐卡指向 `basic_acceptance_artifacts_2026-09-17.json`；保留历史有效结论，不冒充当前连续PVE全模式通过。
- 当前 `PveArtifactInputs.Supports` 覆盖全部25 ID，但只代表存在接入路由。普通资源/附旗入口、材料神器输入、特殊神器、武器专用消费者须分开查。旧22/25已被特殊神器接入覆盖；旧PVE完整双面10/25是当时分批基线，不能直接继承为当前完成率。
- 表中“需映射历史证据”不是判定规则失败，也不是要求重跑该卡。先复用旧原卡模式与消费者，仅当前新输入/真实供给/生命周期未被覆盖时补测。

## 逐ID当前入口与证据

2026-09-29 17:00更新：下表接入缺口已按后续证据修正；旧历史记录与失败报告保留。引用主索引即可，不因记录分散重复测试。

表内索引简称均为本目录 `pve_<简称>_acceptance_index.json`，下节列可定位原报告。

| ID / 原名 | 普通实际输入 | 焚毁实际输入 | PVE证据索引 | 当前基本结论或剩余 |
|---|---|---|---|---|
| items_000 荣誉旌旗 | 附实际部队；被动原攻防加值/名望 | 全体回合加值 | artifact | 普通/焚毁主要消费者已有PVE；轮末真实保留/取回已接，见banner_round_return |
| items_001 恐惧旌旗 | 附旗；选真实单位、敌人及攻击槽停攻 | 选至多三实际敌人 | fear | 物理旗两面已有PVE；宝盒复制这张的多目标另列024；共用轮末真实取回已通过banner_round_return |
| items_002 防护旌旗 | 附实际部队并真实护甲承伤 | 普通Artifact焚毁支路 | artifact_resources | PVE查到普通附旗/承伤；焚毁需映射历史证据，不能据此说未实现；共用轮末真实取回已通过banner_round_return |
| items_003 勇气旌旗 | 附旗后选原单位重整 | 焚毁全部单位重整 | courage | 两面主要恢复消费者已有PVE；共用轮末真实取回已通过banner_round_return |
| items_004 红宝石戒指 | 资源入口原固定红晶/标记 | 原红/黑无限许可 | artifact_resources | PVE此批红焚毁；普通已存在入口，映射历史基本证据 |
| items_005 蓝宝石戒指 | 资源入口原蓝晶/标记 | 原蓝/黑无限许可 | artifact_resources | PVE此批普通；焚毁入口已在，需映射原有限证据 |
| items_006 钻石戒指 | 资源入口原白晶/标记 | 原白/黑无限许可 | artifact_resources | PVE此批普通；焚毁入口已在，需映射原有限证据 |
| items_007 翡翠戒指 | 资源入口原绿晶/标记 | 原绿/黑无限许可 | artifact_resources | PVE此批普通+实际招募后续战；焚毁需映射历史证据 |
| items_008 公正之剑 | 实际手牌材料列表→原攻击 | 焚毁倍率 | artifact | 两面真实击杀已有PVE，star_bow索引另含显示受影响回归 |
| items_009 愤怒号角 | 正式骰色与攻城 | 选原额外0..5攻击/骰/伤 | horn | 两面主要消费者已有PVE；不重复穷举全部随机组合 |
| items_010 无尽金币包 | 原影响/名望→实际村庄招募 | 资源入口固定焚毁 | artifact_resources | 普通实际招募有PVE；焚毁需映射历史证据 |
| items_011 无尽宝石袋 | 两个独立基本色输入 | 焚毁固定五色标记 | artifact_resources | 本批PVE焚毁；普通双选色已有入口，需映射历史证据 |
| items_012 黄金圣杯 | 原治疗2及圣杯来源名望 | 原治疗6及手伤抽牌 | grail | 两面原伤牌/部队消费者已有PVE |
| items_013 智慧之书 | 真实移除材料+同色高级供应引用 | 真实移除材料+同色法术供应引用 | material_artifacts | PVE普通取得后真实出牌；焚毁入口有，需映射历史取得/费用证据 |
| items_014 皓日护符 | 原金标记+实际转基本色 | 原焚毁资源许可 | artifact_resources | PVE普通金转色及真实付款；焚毁需映射历史证据 |
| items_015 黑暗护符 | 选一枚原颜色 | 选三枚独立原颜色 | artifact_resources | PVE普通黑色白昼施法；焚毁多选输入已在，需映射历史证据 |
| items_016 星辰之弓 | 真实弃牌、阶段奖励 | 逐笔转换远程/攻城和倍率 | star_bow | 两面实际攻击消费者已有PVE |
| items_017 指挥旌旗 | 真实指挥标记选择/附部队、招募槽 | 实际单位供应选择/可选解散 | special_artifacts + command_offer_pages | 普通实际招募/跨回合及焚毁第四张实际供给已有场景；主动轮末真实取回已接并验banner_round_return |
| items_018 强韧旌旗 | 附旗、明确下一伤保护 | 战外全体真实创伤治疗 | fortitude | 两面主要消费者已有PVE；共用轮末真实取回已通过banner_round_return |
| items_019 灵魂收割者 | 攻击3并逐敌选合法奖励色 | 原焚毁攻击阶段奖励 | harvester | 两面真实击杀/奖励已有PVE；不照搬旧多抗缺口 |
| items_020 亡君之盾 | 原单笔/分次格挡选择 | 原焚毁单笔/分次格挡 | shield | 四模式同敌多攻击真实格挡已有PVE |
| items_021 德鲁伊法杖 | 真实弃牌；四项/原色/单位/路径 | 两个不同原项、单位/路径/选色 | material_artifacts | PVE普通蓝材料取白晶；其他原项/焚毁有入口与历史有限证据，集中映射后仅补缺失消费者 |
| items_022 启迪头环 | 静态原公共技能；动态专用逐次入口 | 实际公共技能取得并后续正式使用 | headband_progression + headband_dynamic + headband_payment | 动态借用持续补齐至傀儡keep→consume71/119；普通混乱临时色初始化尚待依据，不以焚毁取得冒充普通通过 |
| items_023 法术宝典 | 实际材料+同色法术供应+上半分支 | 实际材料+同色法术供应+下半分支 | material_artifacts | PVE焚毁火攻真实击杀；原单层动态、材料、世界、多单位重生/荣誉分页、多人读取输入已补，见tome_dynamic/tome_materials/tome_world/restore_honor_inputs/mind_responses；不泛化多层复制 |
| items_024 神秘宝盒 | 揭示实际顶神器；借原普通效果后归底 | 物理宝盒焚毁，借揭示原神器焚毁 | special_artifacts | PVE已借荣誉旗焚毁；五武器与材料剑/恐惧多目标硬拒已解除并补box_weapon/box_material_fear_scene证据；复用原卡消费者，不泛化全复制排列 |

## 已定位场景证据（只列引用，不重新执行）

- `artifact`：`AutomationOutputs/PveBattle/20260918_161038_f27be9ac/report.json`（文件存在）。
- `fear`：`AutomationOutputs/PveBattle/20260918_174055_7f2f6ffe/report.json`（文件存在）。
- `courage`：`AutomationOutputs/PveBattle/20260918_195330_42819bf4/report.json`（文件存在）。
- `fortitude`：`AutomationOutputs/PveBattle/20260918_194125_fa8382ec/report.json`（文件存在）。
- `grail`：`AutomationOutputs/PveBattle/20260918_211203_b60d5cae/report.json`（文件存在）。
- `harvester`：`AutomationOutputs/PveBattle/20260918_184248_e2f35e14/report.json`（文件存在）。
- `horn`：`AutomationOutputs/PveBattle/20260918_204019_a01b856b/report.json`（文件存在）。
- `shield`：`AutomationOutputs/PveBattle/20260918_182145_cd5816bc/report.json`（文件存在）。
- `star_bow`：`AutomationOutputs/PveBattle/20260918_223337_664a4b31/report.json`（文件存在）。
- `artifact_resources`：`AutomationOutputs/PveBattle/20260923_062855_9e7e0b2e/report.json`（文件存在）；`AutomationOutputs/PveBattle/20260923_063240_e2fd3da9/report.json`（文件存在）。
- `material_artifacts`：`AutomationOutputs/PveBattle/20260923_065024_a1e46fb0/report.json`（文件存在）；`AutomationOutputs/PveBattle/20260923_065159_d7b49c28/report.json`（文件存在）。
- `special_artifacts`：`AutomationOutputs/PveBattle/20260923_070341_4faa1071/report.json`（文件存在）；`AutomationOutputs/PveBattle/20260923_070618_395b24da/report.json`（文件存在）。
- `command_offer_pages`：`AutomationOutputs/PveBattle/20260929_093238_e7101fd8/report.json`（文件存在）。
- `headband_progression`：`AutomationOutputs/PveBattle/20260923_071408_f70ac98a/report.json`（文件存在）；`AutomationOutputs/PveBattle/20260923_071458_7807d450/report.json`（文件存在）；`AutomationOutputs/PveBattle/20260923_071642_5473f6e1/report.json`（文件存在）。
- `headband_dynamic`：`AutomationOutputs/PveBattle/20260929_082532_ce7c1fc3/report.json`（文件存在）；`AutomationOutputs/PveBattle/20260929_082710_85e22fce/report.json`（文件存在）；`AutomationOutputs/PveBattle/20260929_082841_42bb45ad/report.json`（文件存在）；`AutomationOutputs/PveBattle/20260929_083735_34567de7/report.json`（文件存在）。
- `headband_targets`：`AutomationOutputs/PveBattle/20260929_084910_379d2814/report.json`（文件存在）；`AutomationOutputs/PveBattle/20260929_085034_6ada2f5b/report.json`（文件存在）；`AutomationOutputs/PveBattle/20260929_085142_d67c6535/report.json`（文件存在）；`AutomationOutputs/PveBattle/20260929_085300_69acfa46/report.json`（文件存在）。
- `headband_rally_heal`：`AutomationOutputs/PveBattle/20260929_085709_1a523a2b/report.json`（文件存在）。
- `headband_pain`：`AutomationOutputs/PveBattle/20260929_091115_e01dbc8f/report.json`（文件存在）；`AutomationOutputs/PveBattle/20260929_091225_cc4a5845/report.json`（文件存在）；`AutomationOutputs/PveBattle/20260929_091321_218804a7/report.json`（文件存在）。
- `headband_leadership`：`AutomationOutputs/PveBattle/20260929_092637_558d5cdc/report.json`（文件存在）。
- `headband_shape_fork_flight`：`AutomationOutputs/PveBattle/20260929_094751_79b94c8a/report.json`（文件存在）；`AutomationOutputs/PveBattle/20260929_094925_b466869e/report.json`（文件存在）；`AutomationOutputs/PveBattle/20260929_095050_227a9355/report.json`（文件存在）。
- `headband_social`：`AutomationOutputs/PveBattle/20260929_101151_88767f80/report.json`（文件存在）；`AutomationOutputs/PveBattle/20260929_101323_209f8ffa/report.json`（文件存在）。
- `headband_payment`：`AutomationOutputs/PveBattle/20260929_103649_4bd63602/report.json`（文件存在）；`AutomationOutputs/PveBattle/20260929_104111_1fd5bcd1/report.json`（文件存在）；`AutomationOutputs/PveBattle/20260929_104413_41dee53c/report.json`（文件存在）。
- `headband_unit_payment`：`AutomationOutputs/PveBattle/20260929_105814_5e7c1610/report.json`（文件存在）。
- `headband_outside_unit_payment`：`AutomationOutputs/PveBattle/20260929_111016_f65943d1/report.json`（文件存在）。

## 当前明确基本缺口与下一批建议

12:50增量：第2项六附旗共用轮末取回现已接正式玩家入口及UI，原规则后台7/7；荣誉/指挥差异连续场景99步178断言通过，取回后原旗实际再使用。见 `worklog_2026-09-29_banner_round_return.md`；不再作为未接入口排队，其余缺口保留。

1. **宝盒原单层借武器/恐惧旗主要输入**：`PveJourneySession.SpecialArtifacts.cs:53`明确拒绝宝盒借恐惧旗焚毁多目标，`:57`对五武器008/009/016/019/020落入尚待输入分支。物理神器各自已能用，不代表024借出后能用。下一批复用已有材料、骰额外数、目标列表和盾模式等真实输入，保持物理宝盒身份与被借神器归底分离。这是神器本身原单层功能，不能用“所有复制组合延后”掩盖；宝盒再借头环等多层才延后。
2. **全部附旗共用轮末取回选择UI**：`BannerActions.ChooseRoundReturn`（:170）已有正式选择/取消，`EndRound`（:178）消费该选择，但当前Scripts无调用；包括000/001/002/003/017/018，不仅指挥旗。Session也尚无对应玩家入口。指挥旗取回还涉及其附属单位销毁，不能与普通旗等同处理。普通附着、被动/主动效果及焚毁原通过证据保留，此缺口仅轮末取回选择与真实跨轮消费者。优先复用这个共用后台接口加一次UI及少量代表性跨轮验证，不重跑六旗全部双面。
3. **宝典原法术额外输入**：`PlayMaterialArtifact`读取实际供应并复用BattleCardChoice，但显式可传仅单敌、单单位、路径、TomeSpellOption、材料；没有PveDeedSelection整套多目标/源骰/晶色/多单位/第二材料选择。已有焚毁借火攻有场景，不能声称所有借来原法术的主要输入可用。先按供应中实际主要法术列最小未透传输入，不做全库排列或多层复制。
4. **头环普通剩余真实授权**：以当前HeadbandBorrowResolution专用入口和新索引为准。普通动态已接并有场景：横置/百步、两再生/无惧/重整、痛苦、领导、变形/叉形/飞行、社会普通、魔咒/两极实际卡付款、部队付款和战外药草师。德鲁伊变形额外地图输入今天已后台与场景通过，见 `worklog_2026-09-29_shapeshift_druid_scene.md`；不得继续照搬旧remaining。明确还需核/接翻面普通分支（狂乱/萨满/伪装翻面）、决斗独立目标与延迟奖励、傀儡真实战果/持久库存等；混乱专属状态按原频率/允许性先核。参照 `tmp/headband_remaining_basic_audit_20260929.md` 并剔除后续已完成的社会/付款。大师特殊费用、神器内部等借付款仍未接，但优先原主要独立能力而非复杂叠加。
5. **资源/材料另一面证据整理**：此表标为映射的分支拥有实现且有历史121有限基本通过；先对照历史 caseId与前后状态，不默认重新跑。需要当前连续PVE证据的，可把戒指/护符/袋子焚毁、智慧之书取得法术、法杖其余原项串入相容构筑，真实费用/牌区/消费者成组，保留已通过场景段。

## 入口核对路径

- `Assets/Logic/Runtime/Battles/PveArtifactInputs.cs`：25 ID分组、战外允许与独立颜色编码。
- `PveJourneySession.Artifacts.cs`：普通资源/附旗/圣杯真实参数；`PveBattleSession.cs`：PlayArtifact、PlayHorn、PlayShield、BurnFearBanner、UseFearBanner、UseCourage、ChooseKillCrystal等正式战斗消费者。
- `PveJourneySession.MaterialArtifacts.cs`：实际材料/供应引用、作用上下文finally恢复与原费用/效果复用。
- `PveJourneySession.SpecialArtifacts.cs`：指挥旗、头环专用动态入口、宝盒原身份及明确拒绝分支。
- `Assets/Scripts/Battle/PveBattleController.SpecialArtifacts.cs`、`MaterialArtifacts.cs`：实际面板选择；不把后台方法存在当成已场景通过。

本轮只完成审计文档。原探索、部队/技能全表及所有边界不属于这张25神器表，也未因此扩大当前测试队列。


13:14增量：宝盒五武器和恐惧焚毁已通过有效ID路由原Battle/Inspector，后台7新增+2直接回归。盾普通与号角焚毁连续场景68步118断言、四图和Console双0。原hard-reject分支不再是这些战内UI的执行路径；材料与恐惧多目标仍待宝盒场景验证，不将代表性路线泛化全卡。见 `worklog_2026-09-29_box_weapon_inputs.md`。


13:22增量：宝盒真实手牌材料及恐惧格挡阶段双目标已完成连续场景，132009_6511948a 73步129断言、4图、Console双0。见 `worklog_2026-09-29_box_material_fear_scene.md`；不再将这两类UI列为待验证。下一组宝典必要动态输入见 `tome_remaining_inputs_2026-09-29.md`，9种原法术差距及可复用历史证据已明确。


13:42增量：宝典已接PveDeedInputs现有schema及世界战斗绑定，后台新增5+回归4有效；蓝宣夺及荣誉非首单位连续场景82步153有效断言、4图和Console双0。见 `worklog_2026-09-29_tome_dynamic_inputs.md`。神游/献祭/世界路径新桥场景仍待补；重生多单位、读取多人、荣誉供应超3分页仍缺，不能将代表性通过泛化全部模式。


13:58增量：宝典献祭独立材料/实际晶体消费、神游回收抽回再消费85操作151有效断言；飞翼实际移动费用→正式跨轮地道同守军征服与奖励69操作131断言一次通过。共154步282断言、8关键图、Console双0，后台复用不重跑，无生产修改；见 worklog_2026-09-29_tome_material_world_scenes.md。材料/世界路径桥不再待场景；重生多部队、读取多人、荣誉供应>3分页及其它明确缺口仍待。


14:13增量：共用重生Units原多选及荣誉3张分页已接，后台6新增+1直接回归有效通过。合法两构筑场景共134操作200有效断言、7图、Console双0：第四供应赤袍取得后攻击；两真实单位重生恢复后正式下一回合农民移动、同僧侣攻击，英雄伤1保留，五处实例一致。见worklog_2026-09-29_restore_honor_ui.md和pve_restore_honor_inputs_acceptance_index.json。此前多部队/供应>3缺口已补，森林5/拒绝细项仅后台，不泛化全模式；多人读取/窃取尚未接，下一步依据multiplayer_mind_inputs_audit_2026-09-29.md。


14:31增量：原多人读取/窃取实际响应入口完成，后台新增11+直接回归1一次通过；双英雄两条正式跨轮路线227有效操作357断言、8关键图已查看，Console双0。实际蓝响应弃牌获晶并支付移动、绿色原行动同实例永久偷取后强化移动；无白色展示完整真实手牌、明确不偷取且双方同名牌归属独立。见worklog_2026-09-29_mind_response_ui.md与pve_mind_responses_acceptance_index.json。共用018输入缺口退出队列；宝典多人借用未单独场景，不泛化全复制矩阵。无夹具临时修改、运行测试/代理或push，目标active。下一步有限基本差集核实，重点头环翻面/持久技能的合法借用；原后续地图来源仍缺，不重复已通过范围。
