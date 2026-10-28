# 勇气旌旗：战后重整与创伤独立

原图 `Assets/GameData/cards/items_003.png`：分配给自有部队，每轮一次战外重整此部队；焚毁战外重整所有自有部队。复用 `BannerActions.UseCourage`、`CourageBannerEffect` 与实际神器实例，正式规则未改。

本次 PVE 补原卡附着/焚毁资格、战后附旗部队详情及“勇气旌旗：战外重整”操作。普通附旗不自动重整、不进入已打出区；点击普通重整才消耗本轮次数。战斗中使用普通或焚毁均由正式规则拒绝。战斗结束后保留原部队、手牌及神器次数，不通过重新开局清空创伤来表现重整。

就绪和可以激活是两个状态：受伤部队可以被重整，但伤牌不减少，依然不能激活。场景报告分开保存 `units.ready`、`units.wounds`、`units.canActivate`、`units.courageSpent`。健康/受伤的普通/焚毁共四种场景应分别验证，普通仅改变附旗部队，焚毁才改变全部部队。

后台 `AutomationOutputs/RulesState/20260918_195103_540bf4cf` 五项首次通过。场景证据另见 `vibe_coding/codex/pve_courage_acceptance_index.json`；轮间次数重置保留既有后台证据，本批不计为此PVE场景已完成全轮生命周期。
