# test(cards): audit original card effects in Unity scenes and restore original artwork

## Why

卡牌定义和效果类存在不足以证明游戏规则已完成。需要把原卡面、实际场景操作、执行前后数值和独立规则预期放在一起验收，明确旧实现的缺口。

## What

- 新增基础、高级、法术、宝物4个Unity验收场景，共312个案例，引用125条既有CardSO与原图。
- 使用原ActionSystem及已有宝物效果模块；区分即时分支通过、数值不符、尚未联验，保留真实异常和缺失状态。
- 恢复被历史装饰图覆盖的5张高级牌中文原图，保留GUID。逐张核对125张图与首次导入LFS对象SHA256一致。
- 自动化准备步骤可省略重复截图，失败仍截图；旧配置默认行为保持不变。
- 保存原始报告、执行截图、逐例对照、已确认问题和剩余批次清单。

## How to test

可复现的生成步骤（Python依赖Pillow用于源图审计）：

```text
python tools/build_all_card_verification.py
python tools/audit_card_sources.py
```

用Unity Hub打开6000.6.0f1项目，执行 `Tools/Mage Knight/Original Cards/Run All Batches`，完成后执行：

```text
python tools/summarize_all_card_verification.py
```

实际验收：312案例，932次操作，2746个效果断言，316张截图；75例已测分支通过、111例不符、126例待联验。报告没有把UI success当作整卡规则通过。

回归：91项EditMode测试通过；旧四场景114次操作、477断言、118截图通过；Unity Console双扫描为0 errors。证据位于AutomationOutputs/AllOriginalCards/20260910及本次更新的OfficialCards报告。

原图恢复的可复现命令（执行前应保留本地修改）：

```text
git restore --source ffe6bcf --worktree -- Assets/GameData/cards/advanced_card_000.png Assets/GameData/cards/advanced_card_001.png Assets/GameData/cards/advanced_card_002.png Assets/GameData/cards/advanced_card_003.png Assets/GameData/cards/advanced_card_004.png
```

## Risks

本PR审计旧效果，不修复全部旧规则缺陷。125条记录只有121张有效牌面，3张空图和1张牌背单独标识；32个部队和82个技能条目尚未完成场景验收。法术经现有行动入口执行，宝物直接验证已有效果模块，其完整施法/装备生命周期及后续结算不能据此判通过。

## Related issues

无独立issue；对应用户要求“分批在场景验证所有原版卡效果”。完整差异包括存档基线时，应先合并存档PR或在审查中明确其依赖。
