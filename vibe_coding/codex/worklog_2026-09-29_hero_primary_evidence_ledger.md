# 70 自身技能逐 ID 证据账本

本轮仅整理 `hero_primary_evidence_ledger_2026-09-29.json`，未改生产、测试、配置或共享目标，未调用 CLI/Unity，未重算截图哈希。读取 25 份有限索引及其明确引用的 61 份场景报告；不扫描运行目录大日志。

账本按原 `original_skills.json` 中七英雄各十枚技能形成 70 个唯一 SourceId，排除 pool006。每行保留原名/原规则、所属英雄、合法构筑与操作路由源码锚点、后台有效批次索引、实际场景消费者及采纳步骤。失败报告只引用其索引明确接受的区间，保留 failed 原状态；后续补测不覆盖失败历史。付款技能及忠诚不走通用技能按钮，因此额外人工核对其实际材料/付款/招募/地下城单位步骤。

## 结果及边界

| 账本场景状态 | 数量 | 含义 |
| --- | ---: | --- |
| documented_primary_consumers | 39 | 已有本行所述主要消费者，不代表全部主模式或自然升级流程 |
| partial_primary_consumers | 21 | 已有消费者，保留明确未验主模式/部分扩展项 |
| evidence_mapping_needed | 10 | 实现/后台已有，但本人实际消费者证据未精确闭合；不能报场景通过 |

10 项映射缺口：000:00 黑暗道路、000:03 秘密谈判、000:08 激励、002:00 全军行进、004:08 激励、005:05 荧光灵运、005:08 激励、007:00 快步前行、007:05 无惧痛苦、007:08 激励。应先定向查既有证据再决定补测，不因此重跑 70 项。共享实现或头环借用证据不自动替代同英雄自身发动。

明确事实缺口仍是 001:01 野性盟友与 004:02 神鹰之眼的原新增板块探索费用消费者。已验证攻击/减攻、白昼真实守军揭示、移动等分支继续有效，不把固定七格导航算作探索成功。

旧索引的累计入口数和宽泛 pending 不能直接相加：002:03 公开谈判已有后续真实伤牌治疗消费；自身傀儡格挡及决斗单位贡献失奖已有 persistent_primary 补证；变换身形德鲁伊地图输入已有后续专属 worklog。Krang 自有混乱的轮盘/真实攻击与交涉已验证，普通头环借用混乱缺口属于神器路线，单独记录。

相反竞技面、所有配色及特别组合没有被默认为通过，也没有一概扩成当前合作基本验收的硬阻塞。每行 `allModesPassed` 均为 false，防止将“70 合法入口”误读成“70 全模式场景通过”。

文档校验：JSON 可解析，70 行且 SourceId 唯一，七池各十行；每行至少有一个具体后台/场景索引组。无新代码修改，不触发 Unity 编译或场景复跑。

## 二次定向闭合（取代上方初审差集数量）

`hero_minimal_scene_delta_2026-09-29.md` 给出 10 条逐项结论及集中执行方案。补引旧 HeroSkills 第15步真实夜移动后，黑暗道路已有基本消费者；核对 headband progression 第39/42/47步发现 **同一Tovak已永久取得快步前行，再以本人自有技能使用并移动**，不能将此段误归普通借用。两项不需重测。

四激励复用共用 DrawAndLock/最低名望逻辑和 Norowas 消费场景，原ID差异已有后台；仅各英雄实际入口/奖励的一次轻量补证，不分别新建完整场景。更正初审备注：Wolfhawk 奖励名望1、Goldyx绿标1；原JSON全文一直正确。

当前账本状态：40 documented_primary_consumers、1 documented_legacy_primary_consumer、21 partial_primary_consumers、4 shared_scene_backend_variant_only、4 evidence_mapping_needed。四条真正尚缺主要消费者：秘密谈判、全军行进、荧光灵运、本人无惧痛苦。21 partial 已区分真实探索、独立机制主分支、已有共同消费者的本人入口、参数变体和扩展组合，不要求逐颜色/逐目标穷举。

仅修改本账本/专属文档，未实施新场景。计划安排见上述最小差集文档，执行前仍须核实际原供应/费用/手牌路线。

## Tovak 实际新增场景入账

后台181842新组合1/1及场景182034（48/125）实际通过；007:05、007:08已由主代理目视五图并取得最终Console双0。账本追加 `pve_tovak_recovery_primary_acceptance_index.json` 的实际伤牌、抽牌、技能次数和蓝费消费者，二项移出待验队列。当前状态42 documented_primary_consumers、1 legacy、21 partial、3 shared_scene_backend_variant_only、3 evidence_mapping_needed。不扩大为全英雄模式通过。

## Arythea / Goldyx 实际增量

Arythea实际183516（59/113）及Goldyx旧前缀183703+补验184109（合69/151）已入账。分别更新000:03/08和005:05/06/08；最终Console仍标pending，等待统一收尾。当前47 documented_primary_consumers、1 legacy、20 partial、1 shared_scene_backend_variant_only、1 evidence_mapping_needed。只余全军行进主要消费者映射与Wolfhawk轻量激励入口；其余partial依据仍保留。没有改动其他代理负责条目。
