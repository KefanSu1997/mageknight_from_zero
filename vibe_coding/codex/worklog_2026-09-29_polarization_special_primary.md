# 两极分化：白昼黑色/黑夜金色真实付款准备

状态：独立生成器与独立后台测试已写，**尚未编译、执行或场景通过**。未修改生产、共享config、fixture、随机种子；未运行CLI/Unity。

## 原文与入口

`resources/text_json/skill.json` 原两极分化000:07：每回合一次，白昼黑色可付任一基本色，不能发动强化法术；黑夜金色仅作为强化法术黑色费用。`ManaSkillPaymentActions.ReplaceCost` 分别检查 Day+Black→基本色、Night+Gold→Black+PayingStrongSpell。当前 `ManaPayment` UI 同时提供库存色与源骰选择；本路线选择实际库存，不调整任何骰面。

原 `DarkAmuletEffect` 正常能力可产生一枚选色魔力；白昼同时授予昼黑许可，黑夜仅产生魔力，**不设置GoldManaWild**。所以夜金作为黑费须真正走两极分化，不能靠护符许可伪造。正常使用不移除护符，正式回收原牌后次轮可用同一实体。

## 同玩家两轮路线

原阿瑞希亚等级4名望15，合法仅选择两极分化一个自身技能，无单位。沿用既有HornGold红蓝各1库存。五张实际原牌：行进000、敏捷007、承诺008、法术magic_009、黑暗护符items_015。自定构筑而非标准16牌起点。

1. 正式昼战术/回合，原护符正常选择黑（Red初始循环5次）实际产黑标1。其他三张未用原牌标记正式末回合弃牌。
2. 原032护甲3；敏捷强化印刷白费，通过两极选择 Required_White+Source_Black，实际黑标1→0，白库存保持0，产生原远程3，技能本回合已用。正式击杀名望15→17。
3. 结束战斗、正式回合末全五牌进弃牌，实际空库宣布轮末，EndRound/PrepareRound将同五牌洗回并抽回，正式夜战术及第二回合恢复技能次数。按ID选实际手牌，不依赖洗牌顺序。
4. 同护符正常选择金（Red初始循环4次）实际产金标1；其余行进/敏捷/承诺标记正式弃牌。
5. 原狼骑兵007护甲4；原火焰风暴强化费用红+黑，通过两极 Required_Black+Source_Gold 支付金标1，同时真实支付原红晶1。产生攻城火焰8及原自伤1；实际击杀名望17→20。正式第二回合结束保留真实伤牌1，红0蓝1，五原牌弃牌，两个真实回合/两战且同一玩家。

三张关键图为昼黑付款、夜金付款、第二实际击杀；旧普通对立色或其他技能不重测。

## 新增后台门禁

文件 `Assets/Tests/RulesState/PvePolarizationSpecialJourneyTests.cs`，确切方法：

`MK.Tests.Battles.PvePolarizationSpecialJourneyTests.OriginalAmuletDayBlackAndNightGoldPayActualOppositeCostsAcrossFormalRound`

使用正式英雄技能取得、原五牌、原神器正常效果，真实PveJourney两战和正式轮次，检验同护符/同法术引用、技能恢复、源色实际消费、夜间无GoldManaWild、原红费用另付、伤牌与结算。没有TurnTo骰面、伪造魔力标记或手动改昼夜。起始红蓝各1是与既有场景相同的明确合法自定资源起点，不作为特殊源色。

旧 `PveManaSkillPaymentTests` 对这两个机制有定向方法，但其源骰设置使用固定TurnTo，不能冒充本轮来源场景。本新增测试先由主代理仅运行上述一方法，后台通过后再写场景配置；若失败保留真实错误，不绕开规则或直接重试Unity。

## 可执行入口

生成器 `tools/build_pve_polarization_special_primary_acceptance.py` 默认plan only；`--write`写入既有自动化config，`--from N --write`重放合法成功前缀并跳过其断言/截图。未运行任何命令行测试或编辑器；由主代理串行编译、后台、场景并检查最终Console。


## 后台与实际场景结果

`20260929_190011_18c19a2b/results.xml` 两方法2/2，本路线确切 `PvePolarizationSpecialJourneyTests.OriginalAmuletDayBlackAndNightGoldPayActualOppositeCostsAcrossFormalRound` 实际Passed；另方法是克朗，不重复归到两极。

`20260929_190632_8d1aeca1/report.json` 一次success：89操作142断言。46/81/83三图由主代理逐张目视通过。46步白昼实际黑标1→0、远程0→3、技能False→True，红蓝各1保留；55/56步正式轮1→2/Day→Night、手0→5，58步技能False；81步金标1→0且红晶1→0，黑标一直0，攻城0→8、自伤0→1、技能False→True；83步实际击杀名望17→20，真伤保留。

20/46/55/56/58/81/83/88均hero.id=Arythea且journey.samePlayer=True。场景证明同玩家与真实轮次/牌区，原护符/法术物理SameAs另由后台直接核对，不编造场景卡牌实例观察。末步两回合两战，手伤1、弃牌5、红0蓝1。索引 `pve_polarization_special_primary_acceptance_index.json`。

最终Console pending，等待主代理提供实际最终检查文件；未预称双0。未修改共享hero ledger。


最终补记：主代理已取得 	mp/pve_special_primary_final_console.json 实际两次零错误；对应独立索引finalConsole已更新，当前无运行中场景。
