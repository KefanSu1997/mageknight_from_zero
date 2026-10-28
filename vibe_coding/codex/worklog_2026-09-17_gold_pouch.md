# 2026-09-17 无尽金币包真实出牌与场景验收

原卡 items_010；基础影响力4、名望+2，焚毁影响力9、名望+3。复用原SO、原中文/英文卡面和既有 GoldPouchEffect，不改印刷数值。缺口仅为 CardPlayActions.PlayArtifact 缺少映射，现在接正式实际手牌入口；基础去已打出区、焚毁永久移除、重复或非手牌实例拒绝，均复用正式逻辑。

## 后台先测

隔离交付 tmp/parallel_gold/README.md，12个唯一用例全部符合；首轮 mirror/AutomationOutputs/RulesState/20260917_101653_d8b50089 12/12，两个生命周期改用真实GameEngine后增量 101727_0d3ddf26 2/2。主树同批牌区/回合规则已有变化，因此整合时仅复核实际招募2例和轮次生命周期2例：AutomationOutputs/RulesState/20260917_102158_5780ce39，4/4。

## 可复用场景入口

场景记录初始实体引用，增加 PlayArtifactInstance 与 UnownedArtifact 操作、ArtifactFixtureSnapshot 真实牌区/引用数核对。正例从初始实际实体中选牌，负例特意用不在手牌的另一实体；不向手牌塞假阳性卡。每步记录影响力、名望、总数、不同引用数和各牌区；金币包的可见结果用中文说明原卡0/1位置，并显示真实招募部队的原卡图。

tools/build_all_card_verification.py 定义10例：基础/焚毁数值和重复拒绝、实际回合弃牌/轮末重洗/新轮抽回再用、焚毁不返回、两个同ID不同实例分别使用、实际农民费用4招募与影响力2能力、费用5暴徒失败守恒、非手牌同ID拒绝。原始完整回归库保留。

## Unity 场景结果

主树 Logic/Scripts/Tests DLL 于10:25:27/28更新，执行前Console两次0。

AutomationOutputs/AllOriginalCards/20260917_102637_9e6d204c/items：10/10列明分支符合，226效果断言、167UI断言、49次UI操作、11张本地截图。已观察014生命周期、027同ID双实例和037招募/部队能力截图；实际9→5→7影响力、名望10、原神器永久移除，与原牌面及农民原图一致。全部文本可见性断言符合。10:35后最终Console两次0。

执行曾停在初始截图：状态显示播放中、未暂停、未编译、Unity不在前台。将既有Unity窗口激活后，同一轮立即恢复到后续案例并完成；未重新触发菜单、没有重跑整批。证据支持与窗口前台状态有关，尚未定位具体PlayerLoop/帧末等待机制，不能称已修复调度根因。

focused已剔除本轮10个符合用例，历史1357条：500 passed、849 partial、8无有效原卡定义旧失败；missing=0、有效卡历史失败为空。这是历史分支证据，不能宣称所有现有代码全量重验通过。金币包整卡仍partial：完整游戏交涉/升级奖励等通用组合仍待联验。

## 后续隔离交付队列（尚未整合主树）

- tmp/parallel_fear/HANDOFF.md：恐惧旌旗，随后已整合并完成28场景分支；见worklog_2026-09-17_fear_banner.md，PvP/多段攻击等仍未完成。
- tmp/parallel_amulets/HANDOFF.md：皓日/黑暗护符，随后已整合主树，27列明场景分支符合，3例施法结果显示补验；见worklog_2026-09-17_amulets.md，公共骰及探索/传送等仍待联验。
- tmp/parallel_wrath/README.md：愤怒号角，随后已整合并完成9个分支场景验证；见worklog_2026-09-17_wrath_horn.md，原02..23骰色证据保留不重复。
- tmp/parallel_wisdom/HANDOFF.md：智慧之书，随后已整合并完成31列明分支，见worklog_2026-09-17_wisdom_book.md；市场回合末补位和完整游戏持久化仍待联验。

全部子代理工作只在最小纯源码镜像执行后台；主代理串行Unity场景。没有上传、推送、VPN/系统设置操作或重启。
