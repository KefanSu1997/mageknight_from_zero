在《魔法骑士》里，所有玩家共享的 **公共魔力池（Mana Source）** 与三条“奖励牌堆”——**高级行动牌、高级法术牌、神器牌**——贯穿每个昼夜轮次的开始、进行与结束。下面依照规则书与官方参考表逐一整理它们的 **数量、公开槽位、刷新流程、获取方式**。

## 公共魔力池 (Mana Source)

| 重点                                   | 规则条文                                                                       | 实现提示                                  |
| ------------------------------------ | -------------------------------------------------------------------------- | ------------------------------------- |
| **骰子数量 = 玩家数 + 2**（虚拟玩家不计）           | Setup Step 8                                                               | `List<ManaDie> dice`                  |
| **昼夜合法颜色**：夜间禁止金色，白昼禁止黑色；若违规重掷       | Ref.表“reroll black/gold until half show basic colours”([cdn.1j1ju.com][1]) | `bool IsLegal(ManaDie d, bool night)` |
| **使用-返还-重掷**：每回合可取 1 颗；回合结束强制返还并立刻重掷 | End-of-Turn ① Return mana dice to source and reroll([cdn.1j1ju.com][1])    | `ReturnDie(die)` 自动 `die.Roll()`      |
| **黑/金溢出重掷**：初始布置若黑/金 > ½ 必须全部重掷      | 同上条文([cdn.1j1ju.com][1])                                                   | `ResetSource()`                       |



## 高级行动牌堆 (Advanced Action Market)

| 规格                                             | 规则引用                                                                             | 实现字段 |
| ---------------------------------------------- | -------------------------------------------------------------------------------- | ---- |
| **整堆 44 张**（基础 28 + 扩展）                        | 组件表                                                                              |      |
| **公开槽位 3**：Setup Step 6 “turn 3 cards face-up” | `CardId[] Offer = new CardId[3]`                                                 |      |
| **轮次准备刷新**：弃最底 → 其余下移 → 补 1 张置顶                | Ref.表 “discard lowest, move remaining down, draw new to top”([cdn.1j1ju.com][1]) |      |
| **获得途径** ① 等级提升偶数级任选 1 置顶；② 在修道院支付 6 影响力学习     | Level-up & Interact 条文([cdn.1j1ju.com][1], [cdn.1j1ju.com][1])                   |      |

## 法术牌堆 (Spell Offer)

| 规格                                        | 规则引用                                                      |
| ----------------------------------------- | --------------------------------------------------------- |
| **20 张牌**；公开 3 张，与高级行动同步排列                | 组件表 & Setup Step 6                                        |
| **刷新流程** 同高级行动：底牌丢弃→下移→补 1                | Ref.表同行条目([cdn.1j1ju.com][1])                             |
| **获取**：在魔法塔支付 **7 影响 + 同色魔力**（夜晚强效再加 1 黑） | “pay 7… at mage tower to learn spell”([cdn.1j1ju.com][1]) |

> 夜间发动强效需再加 1 黑色魔力【Spell 牌说明】([cdn.1j1ju.com][2]) — 代码层在 `SpellCard.Execute(bool night)` 内判断。

## 神器牌堆 (Artifact Deck)

| 规格 / 机制                                  | 规则依据                                 |
| ---------------------------------------- | ------------------------------------ |
| **16 张独立牌堆**                             | 组件表                                  |
| **仅作奖励**：战利品、古遗迹、烧修道院等指令“抽奖励数+1，选1，弃1到底” | End-of-Turn 奖励流程([cdn.1j1ju.com][1]) |
| **放置**：选中的神器直接 **置顶牌组**；未选那张放回底部         | 同上条文([cdn.1j1ju.com][1])             |

## 轮次准备阶段的“一键刷新”顺序

1. **翻昼/夜板**，重掷 Mana Source 黑/金检查。([cdn.1j1ju.com][1])
2. **刷新单位**（先弃、补玩家数+2；核心阶段交替补精英/常规）。([cdn.1j1ju.com][1])
3. **刷新高级行动与法术 Offer**（底→下移→补顶）。([cdn.1j1ju.com][1])
4. 发放 **战术卡** 并重置玩家技能/部队。([cdn.1j1ju.com][1])

将以上 4 步封装进 `GlobalGameState.StartNewRound()`，即可一次性同步全部公共资源。

[1]: https://cdn.1j1ju.com/medias/83/f7/a0-mage-knight-board-game-complete-rules-reference.pdf "Mage Knight Board Game Complete Rules Reference - 1jour-1jeu.com"
[2]: https://cdn.1j1ju.com/medias/83/f7/a0-mage-knight-board-game-complete-rules-reference.pdf?utm_source=chatgpt.com "[PDF] Mage Knight Board Game Complete Rules Reference"
[3]: https://www.reddit.com/r/soloboardgaming/comments/s3a7ez/mage_knight_question_taking_mana_dice/?utm_source=chatgpt.com "Mage Knight Question - Taking Mana Dice : r/soloboardgaming"
[4]: https://tesera.ru/images/items/68470/MK_walkthrough_EN.pdf?utm_source=chatgpt.com "[PDF] mage knight"
[5]: https://www.facebook.com/groups/297010437655145/posts/1616952105660965/?utm_source=chatgpt.com "I don't get to roll the black dice again if this happens during the ..."
[6]: https://www.reddit.com/r/boardgames/comments/4pxkkt/a_mage_knight_playthrough_to_help_struggling/?utm_source=chatgpt.com "A Mage Knight playthrough to help struggling players : r/boardgames"
[7]: https://www.facebook.com/groups/297010437655145/posts/1419840235372154/?utm_source=chatgpt.com "When/How do I put Elite units in the offer? - Facebook"
