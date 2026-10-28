# 法术宝典动态输入桥（后台）

- `PveJourneySession.MaterialArtifacts.cs` 在既有path参数后追加可空 `PveDeedSelection inputs`。仅宝典使用既有 PveDeedInputs schema，再将回调真实option交给原 ExecuteArtifact / SpellTomeEffect / PlayFromTome；不改变原免印刷魔力、免白昼强效限制与实际额外费用。
- 真实供应需属于当前旅程；物理神器及同色支付材料不得同时占用所借法术材料。实际物理贡献仍由原神器scope决定，临时法术描述仅用于选择输入schema，不进入牌区或授予拥有。
- 复用世界法术ValidateWorldSpell前检和AttachTunnelBattle后绑定；实际同守军/同Context、真实地图移动、失败前守恒。旧unit/path调用保留，inputs.Unit为未提供unit时的后备。
- 无共享PveDeedInputs改动。可使用已有schema的002/004/006/011/012/018单人/020/023；005保留既有单单位，多单位及018多人明确未补，不声称完整九法术全部输入通过。

## 验证
新增PveTomeDynamicInputsTests共5项：
1. 神游焚毁选择真实两张弃牌，正式回合补牌拿到同实例。
2. 读取非默认蓝晶，实际支付原迷雾并移动。
3. 献祭拒绝宝典本体/已选支付牌材料；真实其它神器换蓝晶并支付原法术。
4. 荣誉号召选择实际第二供应，取得同原UnitCard并发动原能力耗竭。
5. 地道焚毁先拒绝假同ID守军且不弃牌，后使用同真实守军绑定正式PVE战斗；无原印刷绿色/黑色费用。

首轮 `20260929_132642_b8875b8e` 4/5，神游测试把绿色原法术配蓝色支付牌，原规则正确拒绝。仅修fixture后补失败单项及直接受影响4项（原宝典普通/焚毁攻击2、原单敌阶段1、额外费仍支付1），`20260929_132725_f8e31d2e` 5/5。有效新增5/5+旧回归4/4；旧失败保留。初次测试编译的NUnit Does.Not.Contain集合重载和UnitAbilityKind.Heal名称错误均仅修测试后通过。

本轮未操作Unity，场景/Console由主代理接续。没有全库重跑、远端上传。
