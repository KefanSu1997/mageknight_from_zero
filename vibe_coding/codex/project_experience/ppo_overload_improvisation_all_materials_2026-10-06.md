# 效能过载复制随机应变须先备齐全部弃牌材料

实际 v83 seed 367063 的第39步把原随机应变永久去除后复制基础三次，但只有两张其他非伤手牌。两次实际弃牌后留下 remaining=1、空手，无法继续或结束。原41步失败保存于 `AutomationOutputs/BattleRl/20261005_v83_zero_entropy_ppo/audit_7168_6.json`；不得归类为旧的纯伤牌 activity=0 死路或自动取消剩余效果。

规则依据：官方主规则 Deed Cards 6 要求支付额外弃牌/去牌费用才可出牌；Lost Legion 第9页 Composite Effects 明确 Maximal Effect 多次复制为单一效果且遵守被复制效果全部限制。原随机应变每次弃另一张非伤手牌且不抽牌。因此起手必须排除宿主和供体后有三张（基础）或两张（强化）实际材料。此结论仅用于该固定材料、不会抽牌的组合，不泛化为所有动态复制效果的提前材料限制。

- 共用 `OverloadEffect.Validate` 在宿主付款和供体去除前核验全部材料数量；后续仍逐次选择、消费不同物理实体。
- 四项新用例覆盖基础/强化不足材料拒绝、费用及牌区完全不变，以及完整三次/两次累计移动9/10。同ID不同实例亦独立消费。
- 定向 RulesCli 35/35：`AutomationOutputs/RulesState/20261006_002339_1762ed87`。
- 新 DLL 下原39步前缀状态和奖励完全相同，原第39步不再进入合法掩码；强制执行仍拒绝且完整状态不变：`AutomationOutputs/BattleRl/20261006_v84_overload_material_replay.json`。第一次诊断未捕获预期 Illegal action 异常，保留为诊断脚本错误，不改写游戏失败。
- Unity Console 两次零错误：`tmp/rl_v84_overload_materials_console_20261006.json`；真实场景拒绝操作另行记录，Console 不替代场景通过。

规则 DLL 已变化，旧训练实际使用历史保留但不得伪造同版本守卫继承；相同观察/动作布局只允许旧权重作为暖启动，未恢复 Adam 动量。胜率不替代原卡消费者及实际 Unity 验收。

来源：
https://www.mageknight.net/wp-content/uploads/Mage-Knight-Board-Game-Ultimate-Edition-Rule-Book-September-2018.pdf
https://www.mageknight.net/wp-content/uploads/Mage-Knight-Board-Game-Expansion-The-Lost-Legion-Rules-2013.pdf

场景补验：`AutomationOutputs/PveBattle/20261006_003653_229723b3/report.json`，28次真实按钮及9项断言通过，截图027已人工查看：原卡、拒绝原因、四张手牌及原资源保持。原临时配置恢复。此仅拒绝行为场景验收，不扩大为整张卡或PPO最终模型通过。
