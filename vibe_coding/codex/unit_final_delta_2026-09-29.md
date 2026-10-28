# 部队最终差集映射审计（2026-09-29）

本次只读当前`unit_primary_evidence_ledger_2026-09-29.json`、现成小索引/历史范围审计及必要源码；未跑测试、未读大场景日志、未改共享账本/生产/Unity。输出为字段建议，不是新验收。

## 当前计数与真实剩余

台账30原UnitID共98模式记录，实际重新统计与summary一致：91 passed_finite_scope、5 not_scene_verified、1 partial_scope_passed、1 backend_only。四勇者同一招募规则按4个ID列示，不是4项不同机制；98不是测试数或所有边界规则数。

本阶段真正未闭合的正常主要消费者只有两类，共同依赖原后续地图：

| UnitID | 未闭合模式 | 当前证据及所需下一步 |
|---|---|---|
| army_2_009 斥候 | 两格距离探索原新板块 | 当前scene.not_scene_verified、backend.mapping_needed；能力许可已有，不等于真实原板块放置消费。需真实后续原板块来源和正式两格探索，不可用自定单格揭示替代。攻城1、移动2和私看击败奖励等已验范围不重开。 |
| army_008 勇者 | 自然精英招募时整次交涉声誉修正翻倍 | 后台12确切方法已有；未实际Core→新轮精英供应→非零声誉修正真实招募。保留not_scene_verified。 |
| army_009 勇者 | 同上，共享实现 | 复用同12方法；未来一个颜色代表即可，不四颜色重测。 |
| army_010 勇者 | 同上，共享实现 | 同上。 |
| army_011 勇者 | 同上，共享实现 | 同上；已过白勇者实际突袭收费不能替代自然精英招募翻倍。 |

Core依赖再次定向核实：`GameEngine.cs:75`实际Map.Placed有Core才传true，`UnitSupply.cs:31`只有coreRevealed且供给槽i为偶数才取精英。现authored JourneyMap全部Countryside，OriginalMapCatalog初始化A/1/2也全部Countryside。征服塔或要塞不会产生Core，多等轮次也不会自然出现勇者；不能改格子类别、塞Offer或以构筑初选勇者绕过。此为与原板块/正式探索同一阻塞，不应拆成四份重复场景。

## 魔宠已过影响3落表核对

`army_2_004`基础综合模式已正确包含新增证据，**没有漏记影响3**：

- backend.additionalEvidence指向195848_27e5a6fe唯一37断言方法；scene.evidence包含`familiar_base_influence`。
- scene.actualConsumer已写同一实际红调谐魔宠影响3买村庄治疗1、上一战旧伤1→0。
- evidenceCatalog.familiar_base_influence已引独立index及200051_5d478abe report，acceptedZeroBasedRanges为63..127，65新增操作、120断言；不是将128全部操作算新增。
- 独立index status=scene_passed，finalConsole=passed、双0。基础影响3已闭合，退出新执行队列。
- remainingSceneBranches只剩非红攻击3、非蓝格挡4、非绿治疗2；unverified也不再列基础影响3，准确。

其余基础参数不是新的独立正常机制：三项各有025452_e400c660 XML确切真实消费者，非匹配不加奖励的共享公式已由非白移动3及影响3场景消费，攻击/格挡/治疗入口和消费者已由匹配色5/7/3场景验证。保留`partial_scope_passed`作为“精确值并非全都直接场景”说明是诚实边界，不应将3个参数各再建完整批次。不得将其写成各精确值直接scene_passed。

拒绝维持移除是army_2_004另一`backend_only`记录；已有后台，属拒付分支，不阻塞目前正常主要消费者，不重新开场景。

## 逐ID字段更新建议（无需测试）

1. **army_2_004：状态/证据数组/remainingSceneBranches均保留。** `sharedImplementation.rule`存在拼接历史句“只将实际基础移动3记场景通过”又接“影响3现也已通过”；建议整句替换为：“同一公式；非白基础移动3与基础影响3已有直接真实场景；攻击3/格挡4/治疗2保留确切后台参数+共享场景证据，不声称精确值直接场景。”不改变partial状态或模式计数。
2. **army_008、army_009、army_010、army_011：不改scene.status。** 可以为相同mode统一补`sharedDependency=original_core_exploration`及`representativeSceneRequired=one_original_heroes_colour`说明（可只保留现有文字而不增加字段）。保留12方法，不重复断言统计；不得将已过突袭付2影响混作招募翻倍。
3. **army_2_009：保留探索not_scene_verified和backend.mapping_needed。** 这里mapping_needed指“完整原板块真实探索消费者证据尚无”，不是要求再扫旧日志找普通移动许可。可将implementationNote明确为“仅许可已有，完整原板块入口尚缺”；不能把mode.implemented=true解释成完整原探索已接入。
4. **army_014（巫师）及army_2_009的已过免费远程3/攻城1/移动2：不改通过状态。** 关联`pve_remaining_unit_primary_acceptance_index.json`顶层status仍写scene_passed_console_pending，但其console已经two reads,zero errors；实际`tmp/pve_primary_delta_final_console.json`本次只读确为双success/data空。建议主代理修该索引顶层为scene_passed，并修mappingAudit.followup中“最终Console待补”。此为文本状态过时，无需重测。
5. **army_008..011共享突袭付影响2：不改通过状态。** 对应`pve_heroes_assault_fee_acceptance_index.json`status=passed、finalConsole=passed；建议删除mappingAudit.heroesAssaultFollowup中的“最终Console待补”。不是招募翻倍已过。

## 总括映射字段建议

`mappingAudit.status`原“8个待映射已查证”是历史批次，可保留并加“当前唯一backend.mapping_needed为斥候完整原板块探索，已明确为世界依赖”；不要改成所有待映射清零。`summary`现91/5/1/1无需更改。`sharedWorldStatus.naturalEliteSupply`已准确写Core/原板块共同阻塞；city/monastery属于世界服务入口范围，不能仅凭单位能力消费者通过就宣称这些自然招募全部通过。

本审计无新增测试需求；首先保留已通过证据，补上述少量文字一致性。原后续地图来源与正式探索解决前，不重测勇者自然招募，也不制造Core来源。
