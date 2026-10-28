# 2026-09-17 修行：原版材料、供给与两阶段验收

上一批供给换轮已完成，继续真实原卡 advanced_card_027。观察 Assets/GameData/cards/advanced_card_027.png：基础从手牌去除一张行动牌，取得同色高级行动到弃牌；强化支付绿色魔力，取得卡到手牌。未修改原卡文字或素材。

## 修改

- PracticeEffect 先通过既有 ICardEffectValidator 校验实际手牌引用、行动牌类别和原定义、原颜色、实际供给索引与同色高级行动类别。ActionSystem 在支付强化费用之前调用校验。
- 移除 Math.Clamp，非法索引明确拒绝；用 TryRemovePermanentlyExact 将材料放入永久移除区，保留同 ID 的其他实例。Take 继续复用即时供给补位。没有修改公共支付实现或新造规则入口。
- 场景复用智慧之书的材料与供给视图和真实牌区快照，只有显式 playFromHand 的修行新增分支使用该视图。显示原材料和所选供给、实际弃牌区与绿魔力；继续调用正式 CardPlayActions。

## 后台证据

PracticeStateTests 23/23：四色基础和强化8、同ID第二材料与第二供给及补位2、非法材料/索引/颜色/类别/缺费守恒12、取得卡能否立即出牌1。报告 AutomationOutputs/RulesState/20260917_121746_f771c527。首次测试编译因测试源码缺少 System.Collections.Generic 导致 GetValueOrDefault 扩展不可见，补充 using 后通过；没有进入场景反复查后台错误。

## 场景计划和范围

新增22例，连同受此效果修改直接影响的原2例，共24例（不是全卡库回归）。验证真实出牌、四色材料去除与取得卡去向、回合末、同ID材料、指定第二供给及立即补位、非法选择/缺费不扣资源、取得卡后续使用。报告和截图核对结果随后追加。

完整原卡目标保持；未覆盖的跨轮补牌和其他卡组合不标整卡通过。截图和报告只存本地，不推送、不上传、不改VPN或系统。

## 首次场景与针对性修正

AutomationOutputs/AllOriginalCards/20260917_122224_af310997/advanced_card：24例，83次操作、512项效果断言，25张本地截图。UI全部成功，但18例断言符合、6例失败。失败均仅wisdom:discardIds顺序：预期把弃牌时间顺序当成了牌顶优先的接口顺序。

核对PlayerDeck：GainToDiscard和DiscardPlayedCards以Stack.Push入堆，DiscardPile以堆顶优先枚举；因此基础先获得卡到弃牌、后弃修行，读出应是修行/取得卡；强化按修行、取得卡顺序出牌，回合末读出是取得卡/修行。不改变游戏牌区逻辑。补充后台DiscardObservationIsTopFirstAfterActualTurnEnd 6例，AutomationOutputs/RulesState/20260917_122456_bcf59ee6：6/6；本批累计29个不同后台用例通过。

只修正02/04/06/08/22/23的这一个预期键，tmp/check_practice_case_diff.py确认其它所有案例及这六例其他字段未变。复用既有增量审计脚本，原报告目录incremental_audit.json核验并保留18例已通过证据，排除6例失败；原失败报告不覆盖。查看043_advanced_card_027_11_execute.png同ID材料和第二供给，以及067_advanced_card_027_19_execute.png异色拒绝：原卡面正确、材料/取得卡可见、绿魔力与牌区明确，无截断。

只为这6例准备补测，25次操作；未重测18例或全卡库。

## 最终结果

补测 AutomationOutputs/AllOriginalCards/20260917_122612_525de5fe/advanced_card：6/6分支断言符合，25次操作、140项效果断言、7张截图。查看020_advanced_card_027_22_execute.png：原修行/狂怒/火焰箭矢素材正确，强化绿色费用已支付，真实获得红晶1，回合末火焰箭矢与修行在弃牌堆，狂怒永久移除；两步成功、24/24断言且显示完整。

最终合计24个不同场景分支（22新增、2直接受影响旧例）全部列明断言符合，512项不同效果断言。原18项未改分支由增量审计保留，6项修正预期后补测；不能将两轮所有执行数相加当作新增覆盖。29个不同后台用例通过。所有场景仍保持partial标记，整卡未覆盖范围未隐去。

Unity编译检查两次error=0后才执行场景，最终场景后再次取得两次error=0（count=1000/include_stacktrace=true，约12:27）。git diff --check检查本批修改文件通过，仅提示既有CRLF规范化；无暂存或提交。focused队列已清空，当前成功分支保留回归库，不反复执行。
