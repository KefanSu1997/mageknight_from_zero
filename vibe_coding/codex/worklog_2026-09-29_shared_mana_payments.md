# 魔力强化与魔力霸主正式付款/贡献接入

原规则核对见 `tmp/shared_mana_skills_hooks_20260929.md`。本批生产实现完成，后台测试待主代理统一运行，尚不声称通过或场景完成。

- `SharedSkillArea.ManaPayments.cs`：003:09先Arm基本颜色，不耗技能；精确费用成功提交后给本人原色晶和中央标记。失败保留预选；可取消。其他实际玩家Claim后领真实当回合token，技能归还。007:09非金色选择给本人一个token，中央另一个仅标色；实际供能且产生合法资源的原牌完成后加4。
- `ManaPool.TryPayExactColors`：共享回执在全部资源字典提交后发出，避免返晶被付款快照覆盖；计划/预览不发事件，无限魔力不算实际付款。`MovementService.TryMove`穿湖蓝费复用该入口。
- `ActionSystem.PlayCore`：移除旧Overlord猜RequiredCrystals/免费强效判断；从成功付款当次色捕获候选，在CompleteEffect（含正式异步重复完成）对真实卡牌产生的输出加值。零输出不消费中央；多输出保留真实攻击/格挡引用要求显式选择，不优先猜某种输出。
- `BlockAllocation`尾增SourceCard、`CombatPowerPool.AddBlock`保留原来源且排除部队/技能，复用BoostExactBlock/Attack，保留元素、攻击类型和目标限制。
- 主代理接入统一待选择阻塞和正式回合清预选，并关闭旧裸效果入口。共享状态保存在同一SharedSkillArea，未新增复制的玩家状态。

新fixture `PveSharedManaPaymentTests` 10例待验证。覆盖付款失败/原晶返还、两玩家真实token消费、免费与无限魔力、普通与黑强法术、无输出原晶化、原格挡来源、效能过载异步双贡献选择及结束阻塞。穿湖新入口尚未单列专例；未声称所有费用消费者和场景已验证。
