# 连续回合前置：正式轮末牌区守恒

用户00:20要求两小时不用Unity，恢复检查任务unity-2设于02:20。本轮使用PDF技能读取本地`resources/rulebook/rulebook_chinese.PDF`：扫描PDF无可提取文本，仅渲染第1/4/6/11页，没有全本OCR、上传或下载。首次fitz不可用，改用已安装pypdfium2，没有安装依赖。

## 原规则与缺口

- 第4页轮次准备：重整部队但不治疗创伤，洗自己的功能卡、补手牌，再选择本轮战术并执行取得时效果。战术选择不能用虚构占位牌替代。
- 第6页：一次常规回合通常只进行一个行动；牌库空时可放弃回合宣布轮末，并有剩余玩家最后回合。不能任意“下一战”跳过正式回合，也不能单击新轮免费补牌重整。
- 第11页：普通回合结束保留未打出的手牌，可弃非创伤手牌后补牌；轮末手中所有创伤和其他功能卡一起洗回牌库。
- 当前GameEngine.EndRound仅调用ShuffleFromDiscard，遗留手牌不回收，剩余牌库也未参与混洗。因此按此实现连续游戏会保持旧手牌到下一轮，违反原规则。

## 修改与待验证

新增PlayerDeck.ShuffleForNewRound，将实际手牌、剩余牌库和已打出牌汇入弃牌后复用既有洗牌；GameEngine在既有附旗返回及轮次暂置牌返回之后调用。保留全部原卡引用，不触及永久移除或仍附着部队的旌旗；普通回合与既有ShuffleFromDiscard调用语义不变。

新增RoundDeckLifecycleTests三例：手牌/伤牌/剩余牌库原实例一起返回且焚毁不复活；同ID的两张物理卡跨两轮不合并；普通回合保持手牌而轮末才回收。四处旧测试曾将错误的轮末保留手牌写成预期（智慧之书供应、效能过载副本、仪式攻击副本、思维读取另一玩家），已依据第11页改为断言回到本人牌库、身份和数量完整，永久去除/归属断言不降低。

此修改触及共享轮末入口，需定向回归所有实际调用GameEngine.EndRound的用例及新三例，不扩大到全卡库。后续静态核对确认ManaClaimBasicStateTests.CurseCountsTokenAndCrystalSpendingOncePerTurnAndEndsWithTheRound也将未补牌的轮末手牌伤牌数误写为2。现为第五处旧预期修正：记录两张真实伤牌引用，确认轮末各回到牌库一次；失效后的同色消费不产生新伤牌，累计获得伤牌计数不增加，两张原伤牌仍在牌库。这条用例只验证诅咒期限，不宣称覆盖完整新轮战术/补牌流程。

已准备`round_deck_lifecycle_filters_2026-09-23.txt`，包含22个精确测试方法（含新三例；参数化方法展开后的案例数以运行报告为准）。扫描Assets/Tests中的EndRound调用，除此只有独立GlobalResources.EndRound供应测试，不受本次玩家牌库回收修改影响。资源允许后在仓库根目录执行：

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$roundDeckFilters = Get-Content vibe_coding/codex/round_deck_lifecycle_filters_2026-09-23.txt
dotnet run --project tools/RulesCli -- . @roundDeckFilters
```

当前仅生成筛选清单并检查方法匹配，尚未执行上述命令。出现失败时保留报告，修复后只补失败及新修改影响的方法。

尚未运行.NET/Unity编译或测试。用户游戏期间内存偏低，先形成轻量代码与可执行测试，02:20后资源合适时执行；后台失败先修复再走场景，Console双0和实际连续场景均待补，不增加通过数。完整连续控制器、战术来源及新轮合法门槛仍未接好，本修复不等于完整目标完成。
