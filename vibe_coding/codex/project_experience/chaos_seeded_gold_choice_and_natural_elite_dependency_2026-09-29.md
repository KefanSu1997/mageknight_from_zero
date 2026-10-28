# 混乱原种子与金色必要选择；自然精英依赖

后台与场景需读取当前构筑，不能把历史报告的轮盘初色作为现在的事实。当前HornGold实际启用seed0；`HeroSkillAcquisition.Acquire(..., new Random(0))`得到Gold。最初沿用历史Black的新增后台失败已保留，未改seed、骰色或生产规则。

合法到达金选择的可重放路线：同玩家先移动并正式结束回合，未用混乱的可选空转Gold→Blue；两次正式休息分别弃真实原牌，结束回合空转到Green、Black。随后白影响实际招募、红攻击实际击杀，下一回合转Gold明确选择黑奖励，生成`Ranged:ColdFire:1`，加原火焰箭矢红费`Ranged:Fire:3`击杀原护甲4狼骑。奖励为黑不等于轮盘转Black，金标记必须保留。

原承诺为`basic_card_008`，007是敏捷。自动化编写时需核原ID定义/原图，不能按旧记忆写卡名或期待数值；本次运行前已纠正卡号且不改原预期影响2。

勇者自然精英招募依赖实际核心板块：`GameEngine`准备轮次时检查`Map.Placed`是否存在`TileSet.Core`，再由`UnitSupply.StartRound`供应精英。当前原A/1/2及自定演练地图均Countryside，征服塔或要塞不会变Core。此项与原后续板块来源/探索同一阻塞，不能通过改地块类型或预塞供应伪造通过。原12项招募服务后台可精确映射复用，场景依旧未验。

证据：`worklog_2026-09-29_chaos_gold_primary_route.md`、`pve_chaos_gold_primary_acceptance_index.json`、`unit_remaining_scope_after_special_2026-09-29.md`。场景是否通过以索引实际报告为准，不凭路线描述判通过。
