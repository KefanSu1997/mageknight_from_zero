# 2026-09-17 新轮供给换底与多轮实体循环

上一批即时补位已完成后台和9个新增场景分支，属于有效进展。本轮继续原供给的长期循环，未重复原卡全库。

## 原规则与缺陷

观察已有本地原中文规则书第4页“准备当轮游戏”2d/e，图 tmp/feishu_docs/rulebook-04.jpg：高级行动面供最底一张回到高级行动牌库底，其余下移，从牌库顶补到最上端；法术按相同方式。与第5页的取得后立即补位分开。

当前源码的偏差：AdvActionSupply.Refill把最底实体从Offer移除后只记到历史列表，未回备用牌库，长期循环丢失；SpellSupply.Refill同样遗漏回底，且GlobalResources.EndRound先DiscardUnchosen，导致整个法术面供都被换走。

- 两种Refill在移除最底前PutUnder同一个对象，继续复用FillVacancies，保留未选实体和顺序。
- GlobalResources.EndRound移除全部法术回底的调用，只调用一次正常Refill。即时Take/Claim流程保持已修复行为。
- 既有DiscardPile字段明确为轮换历史记录，非独立的实体牌区；实际牌已经回到备用牌库，不能在资源守恒时重复计入此历史列表。
- 未改Generic Deck、PlayerDeck洗牌、部队供给或完整玩家轮次顺序。没有拿只补空位的方法替代新轮换底。

## 后台先测

新增OfferRoundRotationStateTests 9例：两类供给各3轮逐步牌序与最初实体回归2；备用为空时底牌到顶且不缩水/复制2；不足3张时仅循环现有牌2；真实GameEngine.EndRound同步两市场并推进昼夜轮次1；基础/焚毁智慧之书取得的实际卡保持玩家手牌、材料永久移除、3轮后也不回公共供给2。

直接回归上一批领取后立即补位并可再取新牌2例。AutomationOutputs/RulesState/20260917_120305_8507aa5c：11/11，未再运行上一批其他已通过范围。首次应用补丁的中文注释上下文不匹配，工具拒绝整批；检查确认源码未变后改用精确最小上下文，没有覆盖其它代码。

## 场景

CardVerificationCase.useRoundEngine只供明确新场景使用，控制器实际GameEngine与ActionContext共享相同Globals供给；EndMarketRound调用真实GameEngine.EndRound，再同步DayPart。结果记录实际round、逐步市场ID和牌区，而不是按预期伪造新轮。

新增items_013_39～42：基础/焚毁各验证有备用连续3轮、无备用取得一张后连续2轮。每轮同时核对两种供给顺序；已取得卡保留在玩家、原材料保持移除；基础神器回到玩家牌库、焚毁神器不返回。使用原卡图与原卡名显示，避免长ID挤掉结果。未声称完整玩家行动/战术轮序已验收。

实际场景报告：AutomationOutputs/AllOriginalCards/20260917_121102_7fde20cd/items。UI报告success，4个新增分支、25次操作、140项效果断言全部符合，5张本地截图。人工查看006_items_013_39_execute.png与025_items_013_42_execute.png：原智慧之书、狂怒材料及真实取得卡面正确；基础三轮后第4轮Night，焚毁两轮后第3轮Day/红晶1；手牌、永久移除区及两类供给文字完整可见。UI使用真实GameEngine的轮次，未伪造数值。汇总仍为partial，表示本批断言符合但整卡尚有明确未覆盖范围。

编译后及场景结束后各取得Unity Console两次error=0（count=1000，include_stacktrace=true）。Logic DLL已更新至12:07:12，Scripts DLL12:05:33。场景完成时间12:11:34，最终检查约12:13。通过的4例已从focused日常队列剔除，回归库保留。历史索引更新为1455例：500 passed、947 partial、8条非有效牌面资产失败；缺失案例0、有效牌面当前已记录失败0。该历史汇总不是全部原卡或当前代码全量认证，不把947项partial算作整卡通过。

## 仍未完成

完整玩家牌库的轮次洗牌、部队及修道院供给、所有获得卡的效果条件与费用、全游戏持续状态及其他原卡组合仍需继续验证。GameEngine自身的RunOneTurn目前是简化调度，并不执行完整玩家动作；本轮仅验证其中真实EndRound对供给及昼夜的影响，不把组件闭环等同整场游戏完成。

源码、文本和截图本地保存，未推送、上传、修改VPN、重启系统或修改Unity包。
