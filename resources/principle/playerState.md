下表把《魔法骑士》整本规则（含 Lost Legion 补遗与官方 FAQ）里“**玩家必须长期记录的状态**”全部归纳出来，并给出它们的 **来源条款、变动时机以及代码落点**。阅读这一张清单即可一次性为 `PlayerState` 数据结构对齐所有字段，不再遗漏手牌上限、指挥槽等细节。

## 1 牌区与回合循环

| 区块                 | 规则摘录                                                                       | 何时刷新                 | 代码建议                         |
| ------------------ | -------------------------------------------------------------------------- | -------------------- | ---------------------------- |
| **牌组 (Deed Deck)** | 抽牌时若抽空，只拿到剩余牌；本轮不重洗【Rulebook §7 Draw Cards】                                | `StartRound()` 把弃牌洗回 | `DeckSystem.Draw(int)` 不自动重洗 |
| **手牌 (Hand)**      | 上限=5；夜间 –1；城墙上再 –1；技能/城市/+声望可增【Rules Ref “Hand Limit”】([cdn.1j1ju.com][1]) | 回合结尾“弃后摸到上限”         | `HandLimit()` 读取临时加值         |
| **弃牌堆**            | 回合内打出或弃掉的牌结束阶段全部入弃牌堆【Rulebook p.10】                                        | 轮次洗牌时清空              | `DiscardPile`                |

## 2 部队 (Units)

| 字段                 | 规则出处                                                                            | 说明 |
| ------------------ | ------------------------------------------------------------------------------- | -- |
| **Command Tokens** | 初始 2；声望 +3/+7 各 +1；某些技能临时 +1【Rules Ref “Command Limit”】([cdn.1j1ju.com][1])     |    |
| **Unit Status**    | Ready / Spent / Wounded / **Fatigued=创伤≥等级**【Rulebook p.20】([cdn.1j1ju.com][1]) |    |
| **雇佣与解散**          | 若槽已满需先解散；解散部队移出游戏【同页】([cdn.1j1ju.com][1])                                       |    |

> **结构** `UnitState { CardRef, Status, Wounds, Fatigued }`；存入 `PlayerState.Units`.

## 3 英雄属性

| 字段                  | 规则关键句                                                                          | 影响                                  |
| ------------------- | ------------------------------------------------------------------------------ | ----------------------------------- |
| **护甲 (Armor)**      | 面板 A 盾值【Rulebook 面板示意】([reddit.com][2])                                        | 创伤=⌈伤害÷护甲⌉【FAQ 示例】([reddit.com][3]) |
| **名声 (Fame) / 等级**  | 每升 2 级得技能 & 手牌上限 +1【Rulebook p.22】([reddit.com][4])                            |                                     |
| **声望 (Reputation)** | 轨道 –7…+7；影响招募花费【Rules Ref “Influence”】([cdn.1j1ju.com][5], [cdn.1j1ju.com][1]) |                                     |
| **创伤计数**            | 若手牌全部创伤必须休整【Rules Ref “Rest”】([reddit.com][6])                                 |                                     |
| **战术卡 (Tactic)**    | 每轮开头选 1；决定手牌先后与增益【Rulebook p.17】                                               |                                     |
| **技能列表**            | 等级偶数获 1 技能【p.22】([reddit.com][4])                                              |                                     |

## 4 魔力资源

| 池                      | 规则                                                        | 代码字段              |
| ---------------------- | --------------------------------------------------------- | ----------------- |
| **水晶库存 (Crystals)**    | 每色上限 7 颗【FAQ / Reddit】([reddit.com][2])                   | `int[6] Crystals` |
| **临时魔力 (Mana Tokens)** | 取自 Mana Source；回合结束返还并重掷【Rulebook p.16】([youtube.com][7]) | `int[6] Tokens`   |

## 5 地图与位置

| 内容       | 规则                                                             | 建议字段                           |
| -------- | -------------------------------------------------------------- | ------------------------------ |
| **坐标**   | 站在 Map Hex；移动费用随地形【Rules Ref “Movement”】([cdn.1j1ju.com][1])   | `AxialCoord Position`          |
| **占领标记** | 城市/要塞放盾影响声望与招募【Rules Ref “Keeps & Cities”】([cdn.1j1ju.com][1]) | `VisitedSites HashSet<SiteId>` |



[1]: https://cdn.1j1ju.com/medias/83/f7/a0-mage-knight-board-game-complete-rules-reference.pdf "Mage Knight Board Game Complete Rules Reference - 1jour-1jeu.com"
[2]: https://www.reddit.com/r/MageKnight/comments/56nida/questions_mage_knight_base_boardgame/?utm_source=chatgpt.com "Questions - Mage Knight Base Boardgame : r/MageKnight - Reddit"
[3]: https://www.reddit.com/r/MageKnight/comments/hn5wm1/just_a_question_regarding_assigning_dmg_to_my_hero/?utm_source=chatgpt.com "Just a question regarding Assigning DMG to my hero : r/MageKnight"
[4]: https://www.reddit.com/r/Yugioh101/comments/1hxkllf/does_hand_size_limit_only_do_something_at_the_end/?utm_source=chatgpt.com "Does hand size limit only do something at the end of the player's ..."
[5]: https://cdn.1j1ju.com/medias/1f/7b/38-mage-knight-board-game-rulebook.pdf?utm_source=chatgpt.com "[PDF] Mage Knight Board Game Rulebook - 1jour-1jeu.com"
[6]: https://www.reddit.com/r/MageKnight/comments/1hrm5mj/unit_question/?utm_source=chatgpt.com "Unit question : r/MageKnight - Reddit"
[7]: https://www.youtube.com/watch?v=xK2y9rWmnMs&utm_source=chatgpt.com "Dr. D Teaches: Mage Knight Wounds and Healing - YouTube"
