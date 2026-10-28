# Wolfhawk洞悉元素移除的一次实际消费者

独立生成器 `tools/build_pve_wolfhawk_insight_element_acceptance.py` 默认plan：57操作、100断言、5关键图；支持`--write`及`--from`，未写共享配置、不导入写配置旧脚本。唯一新组合测试：`MK.Tests.Battles.PveWolfhawkInsightElementJourneyTests.OwnInsightRemovesActualIceAttackElementThenOriginalPhysicalBlockPreventsWound`。

复用既有Loadout39资源/种子、Wolfhawk Lv6护甲3，仅自身004:05；四张原牌行进000、决心016、狂怒021、火焰箭矢advanced000，合法演练构筑，不冒充原16开局。仅一个技能、四手牌，无隐含分页假设。

真实原寒冰巫妖004冰攻2：格挡阶段先打原决心基础物理格挡2，真实后台preview有效1/需要2。本人洞悉option200只移除所选攻击槽的冰元素，DefenseProfile变Physical而攻击值仍2，原怪GetAttack(0).Element仍Ice，物抗/冰抗都保留。相同原格挡此后有效2，正式防御结算0伤。

随后原红晶支付火焰箭矢3火远程，加原狂怒2物理（物抗低效1）在近战阶段有效4击杀原甲4。正式结束后同一玩家只剩原行进，下一正式回合实际移动2到村庄，洞悉每轮已用仍保留；最终2回合/1战、名望40、0伤、手0库0弃4原牌引用守恒、红0蓝1。

复用`pve_flip_insight_acceptance_index.json`中原实际槽/元素后台及迅捷场景、`pve_wolfhawk_primary_consumers_acceptance_index.json`原物抗场景；本批不重跑其他能力/元素/目标排列、不涉及Regenerate未决数据。`power.attacks`、`attacks.successfullyBlocked`是现有观测键；去色元素本身以原图/敌数值关键截图与后台DefenseProfile核实，不新增生产观测字段。

已读取唯一后台 `AutomationOutputs/RulesState/20260929_195902_5343cd1e/results.xml`，本方法1/1通过、26断言。Unity编译/实际场景及截图尚待，未记场景通过。C#已冻结，由主代理在魔宠路线后串行执行。

实际场景200254_3319e02c一次success57操作100断言，五图29/30/35/43/56主代理已查看。功能消费者有效，但29第三去色按钮y382..427与目标文字387重叠，ui.truncated未捕获，不能宣称visual通过。主代理仅修targeted且三个选项为单列step35/height31，其余布局不变。旧29图和报告保留。
本代理仅新增生成器--display-only：截到29共30操作，0–28纯重放无断言截图，只29一条ui.truncated及一图；plan已核30/1/1，未写config、未改C#。新独立pve_wolfhawk_insight_element_acceptance_index.json记录functional_passed_visual_pending，最终编译/显示/Console待主代理，原57/100不重复计数。

显示补验200906_b688388a已实核success30总操作/1断言，仅29新截图；主代理目视三选项与target/attackSlot已分离可读。原功能57/100保留，额外仅计显示1断言/1图，前置重放不增加功能覆盖；旧29问题图仍保留。修后编译tmp/pve_insight_layout_compile_console.json及最终tmp/pve_glade_insight_final_console.json均实际两次success/0日志。独立索引现scene_passed，不改共享ledger。
