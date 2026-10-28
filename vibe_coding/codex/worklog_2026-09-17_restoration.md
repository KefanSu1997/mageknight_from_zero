# 复原／重生：森林治疗与多部队总等级

原卡 magic_005；已查看 Assets/GameData/cards/magic_en_005.png：基础治疗3、森林5；强化同等治疗，并可重整总等级最多3（森林5）的部队。两个效果均为手形图标、绿色法术；强化按夜间绿色+黑色费用。复用原SO、原卡面、既有法术映射及正式操作入口。

## 实现与后台证据

- RestoreEffect原先直接删英雄伤牌、只处理一个TargetUnit。现治疗写入正式HealingPowerPool，后续消费可分配至实际手牌或按等级治疗部队。
- ActionContext新增ReadyUnitTargets：null兼容单目标TargetUnit，显式空列表表示不选择重整；非空明确列出多个UnitState。重生先校验全部自有/未销毁/不重复/等级有效及总等级额度，全部合法后才付费执行，无前一支已重整后一支失败的半结算。
- 基础复原不重整任何目标；战斗与非法选项在费用前拒绝。重整不移除部队伤牌，治疗和重整分别结算。
- RestorationStateTests基线 `AutomationOutputs/RulesState/20260917_011316_41c26a72` 为3/22符合、19失败；修复后 `20260917_011401_020a5040` 22/22符合。多部队消费补充实际发动攻击后仅重验2个受影响参数例，`20260917_011604_5951fef5` 2/2符合。
- 后台覆盖地形/两种模式、多部队总等级3/5、未选部队不被重整、治疗两支不同原部队、非法总额/重复/外来/销毁目标整笔拒绝、战斗限制、绿黑费用及夜间限制、空选择与旧单目标、真实源卡回合末弃牌和剩余治疗清空。

## 场景准备

测试夹具增加initialUnitWounds及readyUnitTargets配置，将所选索引对应至实际原版UnitState；界面显示原卡、原部队、地形、所选总等级、全体部队就绪/伤牌和费用。多部队流程依次治疗两支原部队，再进入战斗并分别发动原能力，合计攻击4。

范围为22新例+2受影响旧即时例。旧例新增HealHand真实消费，保持原最终伤牌预期；不删除partial限制。相关代码git diff --check通过（只有换行提示），Logic DLL 01:17:13，Scripts DLL 01:17:37晚于源码，Console两次0 error后启动场景。所有工作本地，远端暂停，无Unity重启/批处理/包操作。

## 复原／重生场景结果

`AutomationOutputs/AllOriginalCards/20260917_011808_95858fb2/magic`：24例完整执行，569/569效果断言符合；report.status=success，每个实际按钮步骤和文字适配断言符合。22新例passed，2旧例仍partial。已查看046_magic_005_09_execute（森林总等级5、治疗两支原部队并分别发动攻击合计4、未选部队保持耗尽）与059_magic_005_13_execute（总等级6超过森林5，绿黑费用及全部部队状态守恒）的截图，牌面和数值可读，无裁切。

本批夹具initialUnitWounds可复用补上上一批再生跨两支部队的场景缺口，因此新增advanced_card_015_19/20，仅选择这两例。它们后台对应RegenerationStateTests.ReadiesSelectedUnitButHealingWaitsForSeparateTarget，前批已通过且生产规则未变，故不重复后台。新增界面显示两支部队的就绪/伤牌汇总以便原素材与结果对应。

再生补测 `AutomationOutputs/AllOriginalCards/20260917_012109_89ada101/advanced_card`：2/2场景、37/37效果断言符合，UI报告success。已查看007_advanced_card_015_20_execute：原3级部队就绪但仍有1伤，另一支2级部队被治疗至0伤但仍耗尽；原再生卡面和治疗对象部队卡面与汇总数值一致。补齐原工作记录明确列出的两个部队分离选择场景缺口。

最终Scripts DLL 01:20:13；全部场景后约01:21 Console两次0 error。本批总26个完整场景、606项效果断言符合，当前focused队列已清空通过例，历史失败和回归库完整保留。

历史索引957计划例：373 passed / 576 partial / 8 failed，无缺失及有效卡失败；8项仍为advanced_card_044..047缺有效原定义的旧失败。此汇总不代表所有卡牌或整场游戏已经验收。后续仍需其他治疗来源（能量流转/窃取、旗帜、技能等）、原4级部队完整能力与美术、多人与跨回合联验；本目标保持active。ReadyUnitTargets属于调用方显式选择数据，实际玩法选择器在新指令时须重绑定，不将历史目标当自动选择。
