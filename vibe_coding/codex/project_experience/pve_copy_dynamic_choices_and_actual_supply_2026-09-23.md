# 原复制动态续选与实际供应施法

## 正式入口

过载必须先保存供体原ID与强弱模式，每次依据当前真实手牌、供应和阶段重新选择。复用正式 PendingRepeatedEffect，一次本体出牌、一次供体永久移除，后续失败保留已完成结果；不能预先伪造三次效果。未完成时实际 Advance/BeginBattle 也拒绝推进，不能仅用UI遮挡。

复制输入要区分供体本身与供体效果所消费的材料，并用临时作用域恢复敌人、攻击槽、颜色及材料。血脉借供应强效不取得供应卡，红费和伤牌仍由正式本体效果结算。多层复制尚未接入，明确拒绝，不冒充完整实现。

供应施法选择必须保持与全局供应相同的卡引用。描述对象只用于读取原效果，不进入手牌或已打出区；CardSpellActions 复用旅程注入的原法术定义/ActionSystem，支付正式费用，成功后才消耗同色许可。

## 本地证据与范围

- 后台18项通过：`AutomationOutputs/RulesState/20260923_091234_e3d5d8b1`、`AutomationOutputs/RulesState/20260923_091421_7bed41b2`。
- 同一玩家两回合68步111断言通过：`AutomationOutputs/PveBattle/20260923_091728_7a15bc21/report.json`。蓝许可→实际冰冻停攻，血脉借反戈击杀→真实新伤，下一回合过载宁静三次→实际治疗，永久材料不回弃牌。9张图已查看。
- 最终实际Console双0：`tmp/pve_20260923_copy_final_console.json`。
- 其他强弱分支新Demo场景、复杂供体全部附加输入、供应法术多部队与地图消费者仍待；本批不是全部复制规则通过。证据索引 `vibe_coding/codex/pve_copy_supply_acceptance_index.json`。
