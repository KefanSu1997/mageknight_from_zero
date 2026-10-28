# 鲜血仪式强化与幻术师停攻连续路线（准备，场景待执行）

独立生成器：`tools/build_pve_blood_illusion_stop_acceptance.py`。默认只在内存生成；`--write`显式写配置，`--write --from N`保留成功前缀操作但去断言与截图。静态生成80步133断言，3张关键图。本代理未写AutomationConfigs、未操作Unity、未启动CLI、未改生产或资产。

## 实际构筑及消费者

- 既有Commoners原资源红晶1/蓝晶1，改选择自有幻术师一支。HeroBuild托瓦克等级4、无技能，合法指挥槽内；五原牌basic000行进、basic014耐力、advanced008胁迫、advanced012鲜血仪式、advanced015再生。是合法自定高级五牌构筑，不是标准16牌开局。没有使用ConfiguredHero，因为该入口会清空部队；五牌足以避开HeroBuild正式抽牌窗口。
- 首回合鲜血仪式强化红晶1支付后0，真实受伤1；三行独立选择Green/Blue/White，可选蓝转库存，蓝晶由1至2/蓝标0，绿白各1标。
- 绿标支付原行进强化移动4，真实森林费用3后余1；白标支付幻术师原能力1，对实际无城防哨卫032停攻并耗竭；主代理新增只读`attacks.successfullyBlocked`，路线同时断言原SkipAttackIndices含0、格挡池0、成功格挡集合空，防止把取消当格挡。真实承伤后伤牌仍仪式1。
- 原胁迫基础attack产生攻击3、声誉−1，实际击杀哨卫护甲3；名望15→17，无升级奖励。战斗末清理停攻，正式结束回合，保留库存蓝2、旧伤1和同一耗竭幻术师。
- 次回合原耐力强化支付库存蓝1，蓝2→1、移动4；实际森林邻村庄平原费用2后余2。原再生基础治疗1并重整等级2同一幻术师，正式治疗移除仪式旧伤。正式结束回合清移动，保留声誉−1、余蓝1和同一就绪部队。
- 三关键图：独立三色/蓝转晶选择；白费停攻原敌与同部队状态；跨回合真实旧伤治疗结果。均本地。

## 已核实际后台XML，不重复测试

- `AutomationOutputs/RulesState/20260923_072400_47a3fe82/results.xml`：`PveDeedInputTests.BloodRitualRealWoundAndIndependentColorsPayMovement(True)` Passed，7断言。
- `AutomationOutputs/RulesState/20260923_021816_212ab687/results.xml`：`PveIllusionistSorcererTests.WhiteStopPreventsActualAttackWithoutCountingAsBlockAndExpiresAtBattleEnd` Passed，11断言。
- 原幻术师白晶/影响和真实再生目标、实际村庄旧伤治疗已由上一批护林人/幻术师路线验证，复用，不重新全量跑。

## 待验证边界

此生成器仅通过无副作用生成检查。实际Unity按钮命中、三色原图、费用前后值、Stop后的观测及3张截图必须由主代理执行核定，不能提前记场景通过；失败从对应索引补，不改变规则预期。主代理的只读观测字段变更需按项目要求编译Console双0。
