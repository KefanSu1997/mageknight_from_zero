## 摘要

在《魔法骑士》中，**英雄等级（Fame Track）决定玩家的战力成长与资源上限**，而**声望（Reputation Track）则持续影响玩家在各种地点使用 “影响力” 进行交互时的费用折增**。两条轨道互不相同却又高度交织：等级每升至偶数阶可获得技能与高级行动牌，奇数阶则提升指挥槽、手牌上限或护甲；声望在 +7 到 –7 范围内随玩家各种行为上下波动，对城镇、修道院等招募或购买动作的影响力成本产生 –3 至 +3 的修正。下文分条梳理规则书对两条轨道的全部规定、常见误区及设计实现要点。

---

## 一、英雄等级（Fame Track）

### 1.1 基本结构

* 起始将个人盾标放在 Fame 轨 “0” 格；当获得 Fame 时沿轨向右移动。
* Fame 轨每行代表一个“等级面板”——共 10 级（终局最高可至 11 级于扩展剧本），奇偶行图标不同。

### 1.2 等级类型与奖励

| 等级标识                      | 获得时机                      | 奖励                                     | 规则出处 |
| ------------------------- | ------------------------- | -------------------------------------- | ---- |
| **技能等级** !\[skill icon]   | 越过带「圆牌+书」图标行（2、4、6、8、10…） | *抽两枚个人技能，选一；* 再选一张高级行动牌置顶牌库            |      |
| **指挥等级** !\[command icon] | 越过带「旗帜」图标行（1、3、5、7、9…）    | *获得新指挥槽* → 可招募更多部队；同层面板刷新**护甲或手牌上限**数值 |      |

> *手牌上限*：起始 5，分别在 3、6、9 级提高至 6/7/8 。
> *护甲*：起始 2，亦跟随奇数级板面逐级提升（通常 2→3→4）。

### 1.3 升级流程

1. **本回合结束时才全部结算**（因此不阻塞下一位玩家行动）。
2. 若一次获得多级，需逐级依序触发奖励。

### 1.4 常见误区

* **技能不是牌**：放在桌面，不占手上限；大多“一回合一次”，少数“一轮一次”或被动。
* **高级行动立即置牌库顶**，下回合必定抽到，有别于洗入弃牌。

---

## 二、声望（Reputation Track）

### 2.1 轨道与数值

* 每位玩家盾标从 0 格开始，向右正向为良好声望 (+1\~+7)，向左为恶名 (–1\~–7)。
* 轨道文字标注对应 **影响力修正**：

  * +1 \~ +3 → 影响力 –1/–2/–3
  * –1 \~ –3 → 影响力 +1/+2/+3
  * 处于 “×” 格时无法进行大部分交互。

### 2.2 声望变动事件

| 行动             | 声望变动                 | 说明                       |
| -------------- | -------------------- | ------------------------ |
| 攻击/烧毁村庄        | –1                   | 村庄一次只能烧一次                |
| 攻击修道院          | –1（打怪） / –1 额外（烧修道院） | 怪物获胜 +1 Fame；若烧掉则再 –1 声望 |
| 征服长城要塞         | –1                   | Fortified 敌人             |
| 招募部队 / 治疗 / 买牌 | **实时影响力修正**          | 正声望折扣，负声望加价              |

> **城市交互**：在自己占领的城市，每张己方盾牌提供额外 +1 影响力，与声望加成叠加。

### 2.3 声望与玩法取舍

* 高声望=招募、治疗更便宜，但某些高效牌（如 *Intimidate*）会牺牲声望换高攻击。
* 剩余轮次不多时，可“自由下滑”声望换取短期攻击力或资源。

---

## 三、实现/设计要点

### 3.1 数据结构建议

```csharp
public record LevelTile(int Level, int HandLimit, int Armor, bool IsSkillLevel);

public class FameTrack {
    public int Fame { get; private set; }
    public int Level => CalcLevel(Fame);
    /* 方法实现略 */
}

public class ReputationTrack {
    public int Value { get; private set; }  // -7..+7
    public int InfluenceModifier => Math.Clamp(-Value, -3, 3);
}
```

### 3.2 升级与声望事件

* 在 `EndTurn()` 钩子检查 Fame 是否越过阈值 → 触发 `HandleLevelUp()`.
* 所有声望变动封装 `AdjustReputation(int delta, Reason reason)`，并在影响力计算处随时引用 `InfluenceModifier`.

### 3.3 UI 提示

* Fame、Reputation 轨可用水平进度条并高亮当前格；当达到升级行时闪烁提示“选择技能/指挥”。
* 负声望时在交互面板显示 +X 影响力惩罚的红色提示，以免玩家忘记加价。

---

## 关键参考

* UltraBoardGames - Level Ups 页面，完整列举奇/偶级奖励与流程 ([ultraboardgames.com][1])
* Ultimate Rulebook PDF：Fame 与 Reputation 起始位置及符号解释 ([cdn.1j1ju.com][2])
* UltraBoardGames - 主规则 FAQ 对手牌上限与级别关联说明 ([mageknight.net][3])
* Walkthrough PDF：级别 5 起手牌上限 6 的示例 ([mageknight.net][4])
* Tekeli.li PBF 解说贴：占领据点后手牌加成、指挥槽说明 ([discussion.tekeli.li][5])
* StackExchange 讨论：声望修正适用的所有站点清单 ([boardgames.stackexchange.com][6])
* Reddit 问答：声望如何修改影响力、常见误区 ([reddit.com][7])
* TooManyWords 博客：低声望牌 *Intimidate* 的战略考量 ([taogaming.wordpress.com][8])
* Complete Rules Reference PDF：影响力结算附声望修正表
* UltraBoardGames 规则总览：回合结束抽牌与手牌上限重述 ([ultraboardgames.com][9])

[1]: https://www.ultraboardgames.com/mage-knight/level-ups.php "Mage Knight Level Ups | UltraBoardGames"
[2]: https://cdn.1j1ju.com/medias/1f/7b/38-mage-knight-board-game-rulebook.pdf?utm_source=chatgpt.com "[PDF] Mage Knight Board Game Rulebook - 1jour-1jeu.com"
[3]: https://www.mageknight.net/wp-content/uploads/Mage-Knight-Board-Game-Ultimate-Edition-Rule-Book-September-2018.pdf?utm_source=chatgpt.com "[PDF] Table of Contents - Mage Knight Database"
[4]: https://www.mageknight.net/wp-content/uploads/Mage-Knight-Board-Game-Walkthrough-2012-March.pdf?utm_source=chatgpt.com "[PDF] SCENARIOS - Mage Knight Database"
[5]: https://discussion.tekeli.li/t/pbf-mage-knight/4243?utm_source=chatgpt.com "PBF Mage Knight - Play by Post - tekeli.li forums"
[6]: https://boardgames.stackexchange.com/questions/61110/mage-knight-the-board-game-where-reputation-plays-a-role "Mage Knight The Board Game : Where Reputation plays a role? - Board & Card Games Stack Exchange"
[7]: https://www.reddit.com/r/MageKnight/comments/1d2b22o/mk_boardgame_what_interaction_dose_reputation/?utm_source=chatgpt.com "[MK Boardgame] What interaction dose reputation work for ... - Reddit"
[8]: https://taogaming.wordpress.com/2016/06/17/too-many-words-about-mage-knight-part-v-advanced-actions/?utm_source=chatgpt.com "Too Many Words about Mage Knight (Part V — Advanced Actions)"
[9]: https://www.ultraboardgames.com/mage-knight/game-rules.php?utm_source=chatgpt.com "How to play Mage Knight | Official Rules - UltraBoardGames"
