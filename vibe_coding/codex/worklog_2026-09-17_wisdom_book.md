# 2026-09-17 智慧之书：真实材料、明确供给与取得后出牌

上轮护符已取得场景证据，属于有效进展。本轮整合 tmp/parallel_wisdom 的隔离后台交付；先读取当前源码和原 items_013 牌面，排除 CardPlayActions 冲突上下文后应用局部补丁，并手动补一项正式映射。保留其它卡规则和未提交修改，没有整文件覆盖或远端操作。

## 原规则与必要修改

原智慧之书基础永久去除一张手牌行动牌，从高级行动供给取得同色牌进入手牌；焚毁改为从法术供给取得同色法术进入手牌，再得一颗同色晶体。原图为 Assets/GameData/cards/items_013.png，复用 CardsAssets 与正式 CardPlayActions/WisdomBookEffect。

- 旧实现用 RemoveFromHand 不记录永久移除；找不到同色供给时默认拿第0张。修正为实际持有行动牌引用、原牌颜色与类别、显式供给索引和同色全部校验通过，才永久移除材料、拿取所选供给并加入真实手牌。禁止伪造同ID引用、伤牌、法术、神器本身和未知原行动定义。
- 基础神器进入已打出，焚毁神器进入永久移除；同ID材料双实例只去除所选引用。成功清除 CardToRemove，拒绝保留原选择与所有牌区/资源。
- 焚毁晶体复用正式上限及溢出 token 规则。获得法术不自动施放，也不豁免印刷费用或白昼强效限制；之后用实际新晶体付费。
- 场景配置新增 advancedOffer，空或显式供给可准确指定，未配置时沿用既有默认。控制器增加材料原图、选中供给原图，和真实材料/神器去向、供给剩余、晶体与后续实际数值。快照按实际牌引用区分同ID实例。
- 案例中的 advanced_card_000 原名是火焰箭矢，基础得1红晶；最初标题误写血之仪式，视觉检查后更正，规则/数值/原图没有改为自制牌。

## 后台先行

隔离35个唯一后台用例最终符合（先34/35，原同色法术夹具错误修正后仅补1例），证据详见 tmp/parallel_wisdom/HANDOFF.md，首轮失败保留。

主树仅选6个整合相关例：ExplicitOfferIndexSelectsSecondSameColorAndLeavesOtherActualOffer 2、AdvancedActionMaterialAndEqualIdCopiesPreserveExactIdentity 2、AcquiredAdvancedCanImmediatelyPlayItsOriginalEffect 1、AcquiredSpellPaysPrintedCostFromActualNewCrystalAndDayStrongStillRejects 1。

tools/RulesCli 同源NUnit：AutomationOutputs/RulesState/20260917_112456_e6e7a230，6/6。其它已通过且未受整合影响的隔离例不重复。此后只有场景显示/配置修改，未再运行后台。

## 31场景分支

四色基础/焚毁兑换并结束回合8例；显式第二张同色供给2例；高级行动同ID双实例2例；基础/焚毁各8种非法材料/供给16例；满晶溢出到期1例；所得原火焰箭矢立即出牌再结束回合1例；所得法术先拒绝白昼强效、保留红晶再实际施放基础1例。

Logic/Scripts/Tests DLL 11:30:19/21/21。Domain reload期间本地6400曾拒绝连接，确认进程仍存在、端口恢复后 Console 两次0，没有重启或重复发起运行。只有后台通过后才触发 focused。

首轮 AutomationOutputs/AllOriginalCards/20260917_113229_c59ac9d7/items：31列明分支全符合，817效果断言、105实际UI操作、32张本地截图。每一步断言原材料/供给图绑定和文本未溢出。查看043同ID焚毁、058伤牌拒绝、105原法术实际支付：材料原图、智慧之书原图、所选法术图可同时见到，实体去向与远程5可对应。

视觉检查发现非法选择结果只显示通用“即时断言通过”，需要明确拒绝原因；增加按真实异常生成的拒绝提示及精确 UI 文本断言。并修正一个案例标题的原卡名。只补测16个拒绝例及该标题例，其余14例保留证据。Scripts DLL 11:34:35，补测前 Console 两次0。最终补验结果随后追加。

## 最终补验

AutomationOutputs/AllOriginalCards/20260917_113525_415d05cd/items：17个受文字/提示影响的分支，502效果断言、52实际操作、18截图全部符合；原16种拒绝的实际原因与新增UI预期逐一匹配。观察014伤牌拒绝和052所得火焰箭矢：拒绝原因明确、无误导性执行成功文字，原卡名与卡面一致，1红晶和回合末两弃牌可见。

最终 Console 两次0，focused已清至0。历史索引1442：500 passed、934 partial、8无有效原卡资产的旧失败；missing=0、有效卡历史失败为空。这是历史分支结果，不是全库当前代码重新认证；本批智慧之书31项仍partial，未覆盖范围仍保留。首轮与补验独立保存，未重跑其余14项。

## 未完成范围与后续

后续纠正：原规则第5页规定取得高级行动/法术后立即补位，不是延迟到回合末。本文件下方的“回合末补位”属于当时待核实描述，后续以 worklog_2026-09-17_offer_replenishment.md 的规则核对与实际证据为准。

智慧之书仍partial：市场回合末补位、完整游戏取得牌的持久化，以及未列出的供给交互/原卡组合继续待联验。本批没有调用轮次 Refill 冒充补位，也没有清除未选卡。当前 SpellSupply 已有 FillVacancies，而 AdvActionSupply 仅有轮次 Refill；下一步应按真实回合调度核对两种供给补位，不能仅依赖隔离交付中较旧的描述。

全部隔离神器交付现已整合（金币包、愤怒号角、恐惧旌旗、两护符、智慧之书），但各自未完成的规则范围继续保留，不能据此宣称全卡或全游戏通过。截图/日志留本地，远端暂停；未修改VPN、系统设置、重启或Unity包。
