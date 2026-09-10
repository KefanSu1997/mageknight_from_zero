# 不可用自制卡替代已有正式卡

上一轮将可复用场景接入了新建的七种训练卡（包括火焰斩、数值6的行军/号召）。虽然操作与数值测试通过，仍然偏离用户要求的《魔法骑士》卡牌。测试只能说明给定实现与预期一致，不能证明需求来源正确。

纠正方式：

1. 先检查 `resources/text_json/basic_card.json`、`Assets/GameData/CardsAssets/*.asset`、`Assets/GameData/cards/*.png` 和 `Assets/Logic/Runtime/ActionSystem.cs`。原牌已有数据、完整卡面与效果，不能另造名称和数值。
2. `ActionCardDefinition` 改为原 ActionCardSO + 原 Sprite 的引用，不再暴露 `value/enhancedValue/mana/action` 等自定义字段。CardSpec 携带原 ActionCardData 快照。Adventure 的适配器只把阶段映射到原效果选择参数，再读取原引擎资源池。
3. 决心与狂怒基础均为攻击或格挡2，但强化分别是蓝色格挡5、红色攻击4。不能把一种卡硬编码成只能格挡/只能攻击，也不能认为强化一定适用于基础可用的全部阶段。
4. 原 ActionSystem 在魔力不足时可能退回基础效果。场景确认前先检查目标、阶段、全部颜色费用，失败不付费、不出牌；通过后才调用原引擎。保留原引擎其他调用者行为，避免无关回归。
5. 横置使用显式标记，提供当前行动1，不创造新效果；伤牌不可用。没有 Sprite 时要关闭 Image，避免默认白色方块遮挡状态文字。
6. 新增“原卡ID/名称/文字/耗色/数值/图片引用”测试；实际 UI 自动化核对 Sprite 名称、TMP 牌效文字与资源变化，并做错误预期故障注入。验收预期来自原卡，而不是从当前运行结果自动抄值。
7. 五种原卡各两张的确定性教学牌组应明确标注用途，不冒充某个英雄的标准初始牌组；未接通的复杂效果不得猜测数值。迁移菜单只更新已知五份教学配置，避免覆盖用户新增内容。

本轮验证范围：行进、耐力、承诺、决心、狂怒在四个原场景及额外遭遇切换中的使用。原牌库其他卡保留，复杂效果和正式怪物/部队全量内容迁移仍需逐项接入。工作记录和证据位于 `vibe_coding/codex/worklog_2026-09-10_official_cards.md`、`AutomationOutputs/OfficialCards/20260910`。

归档补充：上一轮“改动文件包”并非全量工作区快照，必须先叠加其原始基线才能生成下一轮差异，否则未变更的meta会被误报新增。`git diff --no-index` 输出用于重写路径时应禁用重命名推断，或完整处理rename头；本轮使用 `--no-renames` 并验证 `git apply --reverse --check`，不实际回退文件。
