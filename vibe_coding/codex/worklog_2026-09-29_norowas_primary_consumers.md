# 诺洛瓦斯本人全军行进与重整治疗：集中连续路线准备

本批仅新增独立生成器及一条共享生产规则后台测试，未改生产、场景、fixture或AutomationConfigs；未运行CLI/Unity。主代理统一执行后台门禁后再运行场景，当前不记通过。

原来源 `resources/text_json/skill.json` 的 `hero_skill_pool_002:00` 全军行进（每回合按就绪无伤部队数，最多3）、`:02` 重整旗鼓（每轮战斗外重整或治疗一实际部队）；原农民 `army_2_006` 是1级护甲3/招募4/移动2。现有 FullMarchEffect/RallyEffect、UseOutsideSkill及实际招募/承伤/移动入口已支持，无需改生产。

计划同一个等级4诺洛瓦斯、两本人技能、六原牌000/008/014/016/021/items010，零预配部队；不是标准16牌开局，也不声称该演练地图是原版探索。

1. 第一回合原行进2实际进村；金币普通影响4招真实公共原农民，名望15→17，农民保持就绪。结束实际回合保留另四牌。
2. 次回合全军按这支真实已招无伤就绪农民产生移动1，加耐力原2实际付白昼森林3。原032攻击4由同农民护甲3承伤，形成部队1伤/英雄1伤；承伤不等于耗竭，保留就绪但因创伤不可激活。真实狂怒2+决心2击杀，名望19。战后本人重整旗鼓选择治疗：移除部队伤而不治疗英雄、不做重整。
3. 第三正式回合原全军次数正常重置；同已治农民贡献1后实际发动移动2并耗竭，保留承诺横置1，共4实际付平原2；三回合结束同部队0伤/耗竭、英雄旧伤1、名望19、两技能。无需跨轮或再次验证已有重整普通分支。

文件：`tools/build_pve_norowas_primary_consumers_acceptance.py` 默认仅构建计划，`--write` 才写配置，`--from N` 清前缀断言和截图。5关键截图：招募、全军实际森林费用、真实承伤、本人治疗、最后三回合结果（实际脚本以capture标记为准）。不改资产或自动调用Unity。

唯一新增后台：`MK.Tests.Battles.PveNorowasPrimaryConsumersTests.RecruitedFarmerMarchPaysForestThenRealBattleWoundIsHealedAndSameUnitMovesNextTurn`。使用真实原单位供给实体与付款，同一Player/Unit，正式三个回合，PlanDamage真实创伤，无直接AddWounds/假伤/未学技能，实际移动态消费；额外同回合全军重复拒绝。旧全军上限/耗竭计数测试 `JourneyRestSkillTests.FullMarchCountsOnlyActualReadyUnwoundedUnitsAndCapsAtThree` 为可复用历史，不默认重跑。无共享生产变化，仅需新增这一个后台用例后串行场景。

当前状态：**已准备，未执行，不能标后台或场景通过**。若Unity界面选牌页/部队面板实际状态不符，只修当前失败段，不扩大回归或改共享规则迁就场景。

## 后台门禁与界面源码校核补充

主代理已串行运行唯一新增用例：`AutomationOutputs/RulesState/20260929_183332_273a72de/results.xml` **1/1，34断言通过**，SHA256 `3da769906246248e783f196f7ecf174fc85baae2378e7dd2bda09eb0728ce634`。Unity编译后Console双0为 `tmp/pve_social_assault_compile_console.json`。没有扩大旧全军/重整回归。

本代理只运行生成器默认plan：**105操作、179断言、5关键图，未写配置**。只读校核 Deeds（12张每页，切神器重置第一页，010在第一页）、HeroBuild（Lv4及00/02按原序取得）、JourneyHand的`@card`解析/普通出牌/横置、JourneySupport的两技能索引及治疗option1/实际部队目标、OwnedUnits原能力2、承伤PlanDamage与Roster切回手牌路径。所用 units.wounds/ready/canActivate、journey.outsideSkillUsed/samePlayer/completedTurns、hero.skills/armor、手牌/牌库等观测键在Journey或战斗合并CaptureJourneyState中存在。战斗开始会清Roster选择，脚本先开部队再承伤，推进不自动切Roster，因此之后一次切回手牌与现代码一致。

当前状态更新：**后台通过、Unity编译门禁通过、场景待主代理运行**。独立测试和生成器已冻结，本轮未更改Assets/共享配置或运行Unity。

## 实际连续场景完成

主代理实际场景 `AutomationOutputs/PveBattle/20260929_184600_b6b066cd/report.json` 一次成功105操作179断言，42/57/66/82/104五关键图已由主代理目视正常；最终Console `tmp/pve_social_assault_final_console.json`双0。本代理仅读取该现有报告记录，未重跑。

索引 `pve_norowas_primary_consumers_acceptance_index.json` 已保存报告/后台/Console哈希和逐关键点身份：从42招募到66承伤、82治疗、95移动能力、102付款及104最终，农民均 `army_2_006:-93510466`，英雄Norowas、playerId0、samePlayer=True。两技能顺序固定00/02；49技能次数True,False，82为True,True，86正式次回合为False,True，90为True,True，证明全军每回合恢复、重整每轮次数不被错误重置。不能把未选择技能时面板outsideSkillUsed=False当作重整恢复。

70技能账本只更新Norowas002:00及002:02对应消费者、证据组和汇总；最小差集两条退出队列。其它未知/扩展/原探索以及allModesPassed保留，不把这条主要消费者通过泛化为70全规则。当前三项状态均完成：已接入、后台通过、场景通过；无生产改动、重复测试或上传。


