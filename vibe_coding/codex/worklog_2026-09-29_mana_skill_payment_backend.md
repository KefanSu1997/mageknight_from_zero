# 魔咒 / 两极分化：当次真实费用后台

日期：2026-09-29。依据本地原技能图与 `tmp/hero_payment_skills_audit_20260929.md`，不联网、不操作 Unity、不推送。

## 实现
- 新增 `ManaSkillPaymentSelection(Skill, RequiredColor, Material=null, SourceColor=null, SourceDie=null, ExtraDie=false)` 与 `ManaSkillPaymentActions.Execute(player, context, selection, formalPaidCommand)`。作用域内仅替代一次实际费用的一枚魔力，finally 清理；预览无资源或技能消耗。
- 魔咒绑定真实手牌引用：伤牌红/黑、非伤牌白/绿；排除出牌本体、既有弃牌/移除及过载材料重复。成功仅用 `TryDiscardExact` 弃选中实体，不产生可留存标记。
- 两极分化支持红绿、蓝白相反色；真实源色 token 优先 crystal，源晶记 `SpentCrystals`，魔力诅咒按支付目标色判断。日黑支持基本四色；夜金仅在正式强法术付款作用域支付黑费。
- 可直接绑定真实公共源骰；预检普通/额外额度与魔法抗拒，整笔费用足额后才取用，保留源骰历史与真实实例，不先生成临时假 token。
- ManaPool 付款/预览共用计划，先验证全部费用、材料与次数，成功后提交。不靠整状态回滚。原 ActionSystem 汇总印刷/附加费用、真实部队费直接复用；无技能时原流程不变。
- 原无目标 `CurseMagicEffect` / `PolarizationEffect` 拒绝，不能制造可保留 token 或全回合 wild。

## 实际证据
1. `20260929_051025_036a4875`：44 用例中 41 通过、3 失败。既有 `ManaPowerBasicStateTests` 与 `PveMageUnitsTests` 25/25 通过。失败为新增测试发现同 ID 材料值相等误弃本体（修正式生产方法为 TryDiscardExact），以及两条测试把原免费火法远程能力当成红费（核原定义后改为能力 1 的红费 6 攻击）。保留此失败证据。
2. `20260929_051154_5192dac0`：新增 `MK.Tests.Battles.PveManaSkillPaymentTests` **26/26** 通过。命令：`dotnet run --project tools/RulesCli -- . MK.Tests.Battles.PveManaSkillPaymentTests`。
3. 覆盖原移动/原火球、同 ID 真实材料、失败原子性、实际日黑普通/额外骰、实际夜金强法术与另一红费联合支付、缺另一费用时不取骰、火法实际红费、四种相反色、重复费用只替换一项、原色晶体统计与被视为色诅咒、预览、整回合次数及正式新回合恢复。

## 边界与后续
- 后台通过不等于 Unity 场景通过；主代理负责 UI / 场景 / 两次 Console 检查。
- 当前只接入先汇总整笔费用的正式牌与单位入口。免费基本效果/内部后付费（如晶化基本效果、欢悦魔晶内部支付）在外层空或不匹配费用处拒绝，不能先产生副作用。神器内部付款入口主动拒绝。需要后续按原消费者接入，不能宣称全部魔力技能消费者完成。
- 夜金强法术目的由 ActionSystem 设置；UI 裸调用 TryPreviewExactColors 不具备此目的，不能用其结果隐藏夜金强法术选择。内部强法术实际付款正确。
- Execute 是同步付款作用域；不把选择延续到异步过载续选，不建立全回合许可。取消只做预览时不会用掉次数。

## Unity 编译定向修复与共享回归
- 主代理 Console 检出测试程序集无法写 internal setter `ForgoSourceDiceThisTurn`。测试改为真正持有并发动魔法抗拒横置3建立禁令，不放宽生产可见性。仅补 `SourcePromiseOrNoExtraQuotaRejectBeforeCommand`：`20260929_051353_9d03ea9c` **2/2**。
- 共享付款计划器影响的最小补回归：PureMagic 的基础/强效选色加印刷费用2例、同色双费不足保留1例、CrystalMastery 的既有/自身/后续实付魔晶整回合退款及下回合失效1例。`20260929_051430_7c349d88` **4/4**。旧 ManaPoolTests 名称虽吻合，但主要测试源骰，不因此整夹具重跑。
- 生产与测试源码冻结，CLI已交还主代理。场景结论由主代理后续记录。
