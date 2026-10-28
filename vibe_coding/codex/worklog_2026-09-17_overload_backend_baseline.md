# 效能过载：原卡后台失败基线

## 来源与范围

原卡：`advanced_card_024`。重新查看 `Assets/GameData/cards/advanced_card_en_024.png`：去除另一张实际手牌行动卡；基础重复其基础效果三次，红色强化重复其强效两次且免该材料的印刷强化费用。宿主红色费用仍须支付。

官方 Ultimate Edition Rulebook 第5页“Composite effects”（PDF页索引4）：整体作为一个效果，必须遵守被使用效果的限制并保留原色；明确举例效能过载＋决心不能把格挡用于不同敌人攻击。来源：https://www.mageknight.net/wp-content/uploads/Mage-Knight-Board-Game-Ultimate-Edition-Rule-Book-September-2018.pdf 。本次通过网页工具读取，未另下载到项目。

新增 `Assets/Tests/RulesState/OverloadStateTests.cs`，通过正式 CardPlayActions/ActionSystem、原JSON和实际牌区验证，不另写模拟规则，也不改生产实现来匹配预期。

34个后台用例覆盖：8种原材料/模式数值与宿主费用、8个本能原版互斥选项、任意效果编号注入、同ID基础牌按引用移除及换轮守恒、9种非法材料、2个非法原卡选项、宿主缺红费用、宁静战斗禁用两模式，以及重复决心格挡一次实际原怪攻击后不能保留给第二目标。

这些用例不是本卡的完整组合验收。每次重复选择不同选项、每次额外材料与费用、嵌套借用、获得许可的重复次数和后续使用仍需要正式接口与独立用例；不得据此把整卡标为通过。

## 首轮证据

命令：`dotnet run --project tools/RulesCli -- . MK.Tests.Cards.OverloadStateTests`

报告：`AutomationOutputs/RulesState/20260917_140301_86fe58be/results.xml`，对应 `summary.json`。34例，3通过、31失败、0跳过；执行0.558秒。未再次执行这些用例，尚未运行场景。

确认的实际缺陷：

- OverloadEffect把option转换为ActionEffectId，可以让行进材料产生狂怒强效；默认option=0碰巧执行Move2，造成少数数值假阳性。旧场景手动注入FuryBase/FuryEnhanced，因此无法证明效果来自材料。
- 材料没有有效原行动定义、类别与实际持有引用验证；外来实例、伤牌、法术、神器及占位卡可被接受。
- RemoveFromHand按值移除，同ID基础牌可能移除未选中的那一张；没有进入RemovedCards，破坏实体守恒。
- 材料为空时抛错发生在ActionSystem付红之后；非法内层选项还可能抛KeyNotFoundException，而非付款前明确拒绝。
- 原材料的模式、互斥选择与使用限制未转交原效果，宁静在战斗中也没有拒绝。
- 重复决心未生成应有的格挡10，现有代码甚至未进入正确原卡效果。

3项通过为宿主自身不能当材料、缺宿主红费拒绝、以及本能基础移动选择0的即时数值。最后一项只是与错误的默认Move2恰好同值，不构成原卡映射正确的证据。

## 编译与下一步

首次构建新增测试时发现本项目NUnit没有Assert.Multiple，并补齐ActionEffectId所属命名空间；已改用仓库兼容的逐项断言，不升级依赖。之后后台成功编译并得到上面的真实规则失败结果。

刷新Unity后，实际 `Library/ScriptAssemblies/MageKnightTests.dll` 时间为2026-09-17 14:03:33；Unity Console按types=[error]、count=1000、include_stacktrace=true连续两次返回0条。生产源码、场景和现有验收配置未修改。

后续修复必须从实际材料解析正式原效果、按引用永久移除、在宿主费用前验证内层选择，并处理重复效果的各次选择与材料。不要继续沿用任意效果枚举，也不要为了让基础数值通过而禁止合法原行动材料或把有额外选择的卡当作普通加值重复。后台相关范围通过后再进入对应场景；当前31个失败保留，不更新为历史场景通过。

本次没有推送、上传截图或日志，没有修改VPN、系统、Unity包，也没有启动Unity批处理。目标继续运行。
