# Krang 本人混乱金色明确选择与冰火远程消费者

准备完成，待主代理后台和Unity串行验证，不记已通过。独立生成器 `tools/build_pve_chaos_gold_primary_acceptance.py` 默认plan：105操作、184断言、5关键图；只有 `--write` 才落共享配置，支持 `--from` 增量补验，不导入旧版执行即写配置脚本。

独立唯一后台方法：`MK.Tests.Battles.PveChaosGoldPrimaryJourneyTests.SeededOwnChaosWhiteRecruitRedAttackThenGoldColdFireRangedConsumer`，源文件 `Assets/Tests/RulesState/PveChaosGoldPrimaryJourneyTests.cs`。本代理不运行CLI/Unity、不改生产和fixture。

## 原始依据与最小连续路线

- 沿用已有构筑39（seed0，红蓝库存各1，无预配单位），Krang等级6、护甲3，仅取得自身003:07，不超过3技能名额。`HeroSkillAcquisition.Acquire(...new Random(0))` 实际掷骰初Gold，禁止赋值初色或改fixture。
- 六张已实现原牌：行进000、宁静001、承诺008、耐力014、狂怒021、火焰箭矢advanced000。合法演练起点原牌，不冒充完整原16牌开局。固定六牌不超过一页；只选一个本人技能，不再假定多技能页的隐藏按钮可见。
- T1行进基础移动2到现有演练真实村庄，正式结束并空转Gold→Blue；T2正式休息弃真实宁静001，结束空转Blue→Green；T3正式休息弃真实耐力014，结束空转Green→Black。三回合不发动混乱、不直接赋色。`PveJourneySession.Rest` 与 `ChaosSkillRules.ResolveUnusedRotation` 为正式入口。
- T4承诺基础影响2、混乱Black→White影响2；正式交涉按原费用4招真实供应农民army_2_006。正式回合结束，原两牌入弃牌区。
- T5同玩家对原032（攻击4、护甲3）真实承两伤；混乱White→Red攻击2加原狂怒基础2击杀，正式结束，不空转。农民保留同实体且未代受伤。
- T6仍同玩家，当前Red。对原狼骑兵007（护甲4、名望3），先转Gold明确选option2黑奖励，实际贡献 `Ranged:ColdFire:1`，轮盘保持Gold；支付原红晶给火焰箭矢强化 `Ranged:Fire:3`，两笔合4真实远程击杀。四次正式剩余阶段结束，无新伤。
- 最终六回合两战，同一玩家/农民，轮盘Gold无待空转；名望40、真实伤2、手2/库0/弃6，总六原牌加两伤，红0蓝1。测试以引用核原牌/旧伤/单位保留。

`ChaosSkillRules` 的Cycle次序为Blue/Green/Black/White/Red/Gold。金色是下一色Gold时显式选五奖励，option2为Black；不是先将轮盘设置Gold后使用（那样下一次会转Blue）。实际枚举ColdFire与AttackType.Ranged及观察键`power.attacks`均核源码；战外使用`journey.outsideSkillUsed`与`skills.chaosColor`，战内使用`skills.used`。不借用普通头环，不把本人证据扩为神器借用通过。

## 复用证据与新组合门禁

复用 `pve_social_persistent_acceptance_index.json` 原取得轮盘、白影响/红攻击、正式未用空转证据；后台 `PveChaosSkillTests.GoldExplicitFiveRewardsKeepGoldMarker` 和 `BlackIsActualColdFireRangedContribution` 已有金选择与类型证据。本次只新增一条真实连续组合，不重跑全部六色或金五选项。

主代理运行前审阅指出最初误把007敏捷当承诺。已在测试和生成器实际构筑/出牌改为原008，保留原影响2预期，未修改正式规则；这是静态纠错，不存在该版已通过报告。

后台结果、实际场景/图、Console均待主代理。生成器冻结后无额外共享修改。

首后台 AutomationOutputs/RulesState/20260929_193416_6420e9c9/results.xml 失败在25行：当前实际Random(0)初Gold，旧场景Black不能照搬。保留失败，未修改种子/初色/规则。改为上述六正式回合，通过真实原牌移动与两次休息建立三个合法未用空转窗口。测试和生成器同步，后台仍待仅重跑本方法；不把修正当通过。

复验已实际核实：AutomationOutputs/RulesState/20260929_193838_08b01bcf/results.xml，新六回合方法1/1通过，43断言。主代理核实当前HornGold.asset启用replayEffectRandom=1且effectRandomSeed=0。独立pve_chaos_gold_primary_acceptance_index.json已保存成功及原失败报告哈希；仅后台通过，实际Unity场景/截图和最终Console仍pending。本次仅记录，不改代码/config/ledger。

最终场景已核实：AutomationOutputs/PveBattle/20260929_194206_5c29a0ca/report.json，一次success105操作/184断言，无失败。68/92/93/99/104五关键图主代理全部目视通过。68招募到104最终，原农民实例始终army_2_006:-2080709720；93实际Gold及Ranged:ColdFire:1，原红费火焰箭矢补足后99真实击杀，末态samePlayer=True、Krang、六回合两战、名望40、Gold无空转待处理、手w,w/库0/弃6、总8，红0蓝1，农民就绪无伤。
编译tmp/pve_chaos_gold_compile_console.json与最终tmp/pve_chaos_gold_final_console.json均实际读取两次success/0日志条目。独立索引scene_passed，成功/失败后台、场景、Console路径哈希和同实例核查均保留。未改ledger，不扩为全部轮盘排列或普通头环借用通过。
