# 原战术进入真实回合/轮次

## 修改与依据

依据本地12张原战术图（精确映射见 `pve_tactics_original_audit_2026-09-23.md`），新增 `OriginalTactics` 定义与 `TacticState/TacticActions`，通过已有 `GameEngine` 调度取得时、回合前、回合中及回合末效果。保持同一玩家、手牌/牌库、公共骰与部队实例，不生成训练卡。

- 三思后行先抽后洗弃牌；午夜冥想先洗指定手牌再抽。选择按实际引用，允许伤牌，禁止重复材料。
- 周密计划在回合末根据实际保留手牌决定本次补牌+1；完美开端直接抽2；万全准备选择实际牌库中的一张。
- 魔力窃取的原骰单独保留、以后实际使用、当回合末归还重掷，不占普通取骰额度；搜寻按金骰优先、每回合一次和取用源前验证。
- 最佳时机复用真实额外回合；漫漫长夜回收实际随机弃牌；积蓄力量独立卡下区按回合前选择，轮末保持牌实体守恒。
- 魔力骰原实现错误排除了白天黑面/夜间金面，导致原搜寻的“优先金色枯竭骰”没有正常来源。现在重掷保留原六面，取用颜色仍由正式消费规则限制。相应风暴回归预期由5面修为6面，并新增实际枯竭结果及拒绝使用验证。
- 战斗未结束不能直接结束游戏回合；未完成战术取得选择不能开始回合。

## 验证

`dotnet run --project tools/RulesCli -- . MK.Tests.Cards.OriginalTacticsStateTests MK.Tests.Cards.RoundFlowBoundaryTests MK.Tests.Cards.MagicFamiliarsStateTests.NextRoundRequiresInventoryCrystalBeforeTacticsAndPreservesSameWoundedUnit MK.Tests.Cards.ManaDrawPullBasicStateTests MK.Tests.Cards.ManaStormBasicStateTests MK.Tests.Cards.ManaClaimBasicStateTests`

45/45 通过，0失败，0跳过，0.728秒。证据：`AutomationOutputs/RulesState/20260923_031700_b7579b25`。仅新增及共享轮次/骰子的直接受影响范围，未重跑卡库。

刷新时MCP因域重载断帧，未重复请求编译；重连后实际 Unity Console 两次0错误，证据 `tmp/pve_20260923_tactics_console.json`。

## 完成口径与后续

本批为原战术正式后台基础能力通过。尚无战术Unity原图绑定、取得/回合前选择UI或集中连续场景证据，不能记为12张完整可玩/场景通过。漫漫长夜在自动补牌中途的交互暂停入口仍须在连续控制器中处理。此前部队场景19/30、神器10/25、技能17/70保持；现有121/121有限原卡历史证据保留。接下来接同一玩家的持续会话与集中操作入口，继续active目标。
