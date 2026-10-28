# 法师塔与蓝城真实法术购买后台（待运行）

2026-09-29。复用已核 place_010 / place_023 及原规则第8页：7影响力加1枚同色基本魔力，取得法术置功能牌库顶，立即补位。本代理只修改后台/新测试，未运行 CLI/Unity。

## 修改

`PveJourneySession.SiteInteraction.cs` 新增地理查询 `CanBuySpellAtCurrentSite` 与 `DeedCard BuySiteSpell(SpellCard offered)`。

使用已有 RequireSiteInteraction 正式进行中回合、无战斗/待选效果、同实际地点引用/位置、已开始且未结束交涉门槛；仅当前已征服法师塔或蓝城允许。先验证同 Globals.Spells 供应与精确 Offer 引用、原 SpellCardData 基本颜色、7影响力，再走同 ManaPool.TryPayExactColors 整笔同色付款（临时标记优先、库存晶体、已有原付款/共享规则）。失败不先扣影响或领取。成功扣7，SpellSupply.Claim 取得原供应记录并立即补位，建立实际玩家 DeedCard 放牌库顶，返回该实体供后续观测。未选供应引用保持不变。没有调用旧 Interactions 路径，也没有将购买误作施法奖励/次数。

## 待验证

新 `PveSiteSpellPurchaseTests` 共11项：
- 塔/蓝城两种地点原承诺真实影响 → 蓝晶付费购买原风雪 magic_015 → 同取得牌置顶并正式回合末抽入 → 下一回合同实例寒冰远程5实际击杀。
- 同蓝城两次购买，两个城市盾徽只加一次，原影响卡/横置实际生成16，支付14，真实新牌叠放顺序及供应立即补位。
- 八类付款前拒绝：同ID替身、影响不足、同色魔力不足、白城、关闭交涉、位置漂移、替代法术供应、未开始交涉。均断言同供应/资源/牌库不变。

建议仅跑新类，及 `PveNaturalSiteRecruitmentTests.PositionOrSiteReferenceDriftRejectsRecruitBeforePayment` 两参数作为相邻地理 gate 的有限回归；未修改原共享招募/法术/付款核心，不重复此前46项。

未运行测试、Console 或场景，不能计为通过。修道院、其它城市购买及补精锐仍是独立待完成范围。

## 首次后台失败修正

主代理结果 `20260929_173632_0f5e5129` 为13/15；失败仅上述两参数真实施法消费者，错误“缺少原卡定义”。Start夹具仅传 basic+advanced，与Unity OriginalDeeds包含法术的实际目录不一致。现改为同现有PveWorldSpellInputTests方式，从原SpellCardData包装ActionCardData加入目录；没有改生产或预期。保留原失败报告，只需补这两个消费者；本代理未运行复测。
