**结论先行（一句话）**：
在《魔法骑士》里只有 4 种战斗元素（物理／火／冰／寒火），却有 6 种魔力颜色；其中 **红、蓝**与火／冰元素一一对应；**绿、白**用于移动-治疗-影响并不产生元素；**金**在白昼可当任意基本色，而\*\*黑色魔力只能在夜晚用于「强效法术或牌面特别注明的效果」，\*\*并 **不能** 像金色那样在夜晚充当万能色 ——这是规则书常被误解的一条。下面逐色梳理其含义、昼夜可用性及典型用途，并附权威条款出处。

---

## 1  六色魔力一览

| 颜色                 | 规则书定位 | 昼夜可用性                                                                        | 典型用途                                                            | 关键出处 |
| ------------------ | ----- | ---------------------------------------------------------------------------- | --------------------------------------------------------------- | ---- |
| **红 (Fire)**       | 基本色   | 日／夜皆可                                                                        | 火系攻击、火格挡、毁灭类法术                                                  |      |
| **蓝 (Ice)**        | 基本色   | 日／夜皆可                                                                        | 冰系攻击、冰格挡、防御控制                                                   |      |
| **绿 (Nature)**     | 基本色   | 日／夜皆可                                                                        | 高移动、治疗、自然召唤等 ([unofficialmageknighttheboardgame.fandom.com][1]) |      |
| **白 (Holy/Light)** | 基本色   | 日／夜皆可                                                                        | 影响力、光系守护、外交牌 ([unofficialmageknighttheboardgame.fandom.com][1]) |      |
| **金**              | *特殊色* | **仅白昼**：可当任意基本色；夜间视为非法且须重掷源池骰 ([cdn.1j1ju.com][2])                           |                                                                 |      |
| **黑**              | *特殊色* | **仅夜晚**：①为法术“强效”支付的必需额外色；②少数牌/地形注明可用；**不能** 通用替代基本色 ([sneakymeeples.com][3]) |                                                                 |      |

> ‼️ **常见误区**
> 许多网络摘要把黑色说成“夜间万能色”。规则书原文：“During Night Rounds, **black mana can be used to power some effects**.”（夜间黑魔力可用于触发某些效果）——指的是「强效法术」及个别牌，而非任意卡牌 。Sneaky Meeples 的勘误亦强调“黑色仅供强效，不是通配” ([sneakymeeples.com][3])。

---

## 2  黑、金与昼夜流程的交互

### 2.1  公共魔力源池（The Source）

* 每轮开始掷 **玩家数+2** 颗骰，必须保证 ≥50 % 为基本色；若不满足则**反复重掷黑/金骰**，白昼重掷黑色，夜晚重掷金色 ([cdn.1j1ju.com][2])。
* 回合内玩家从源池取用后须在回合末重掷并归回。若昼取黑或夜取金（被视为“枯竭”），规则禁止 。

### 2.2  强效法术支付

* 施放法术：白昼用“对应基本色”得基础效；夜晚若要强效，**必须额外再支付 1 黑魔力** ([cdn.1j1ju.com][2])。
* 因此黑色在夜晚最常见的用途即“附加强效”。

### 2.3  牌／技能修改

* 某些神器（如 **Mana Storm**）或技能（如 **Polarization**）允许临时把黑/金当任意色，属“牌面特例”并不改变通则 ([unofficialmageknighttheboardgame.fandom.com][4], [unofficialmageknighttheboardgame.fandom.com][5])。
* 地形“魔法空地”在夜晚给予黑魔力，但仍只能强效或牌面使用 ([unofficialmageknighttheboardgame.fandom.com][6])。

---

## 3  设计与实现提示

1. **ManaColor 枚举**

   ```csharp
   public enum ManaColor { Red, Blue, Green, White, Gold, Black }
   ```
2. **合法性检查**

   ```csharp
   bool IsManaLegal(ManaColor c, bool isDay) =>
       c switch {
           ManaColor.Gold =>  isDay,
           ManaColor.Black => !isDay,
           _               => true
       };
   ```
3. **通配判定**

   ```csharp
   bool ActsAsWildcard(ManaColor c, bool isDay) =>
       (c == ManaColor.Gold && isDay);
   // 黑色从不返回 true；若要通配需牌面特例
   ```
4. **强效检测**

   ```csharp
   bool CanPaySpellStrong(ManaColor main, ManaColor? extra, bool isNight) =>
       isNight && extra == ManaColor.Black && main.IsBasic();
   ```

这样既符合规则书，又避免了“黑=万能”的误读，保证战斗与法术结算正确。

[1]: https://unofficialmageknighttheboardgame.fandom.com/wiki/Mana_Pull?utm_source=chatgpt.com "Mana Pull | Unofficial Mage Knight - The Boardgame Wiki - Fandom"
[2]: https://cdn.1j1ju.com/medias/83/f7/a0-mage-knight-board-game-complete-rules-reference.pdf?utm_source=chatgpt.com "Mage Knight Board Game Complete Rules Reference - 1jour-1jeu - 1j1ju.com"
[3]: https://www.sneakymeeples.com/2016/11/mage-knight-overlooked-rules.html?utm_source=chatgpt.com "Sneaky Meeples: Mage Knight - Overlooked Rules"
[4]: https://unofficialmageknighttheboardgame.fandom.com/wiki/Mana_Storm?utm_source=chatgpt.com "Mana Storm | Unofficial Mage Knight - The Boardgame Wiki - Fandom"
[5]: https://unofficialmageknighttheboardgame.fandom.com/wiki/Polarization?utm_source=chatgpt.com "Unofficial Mage Knight - The Boardgame Wiki - Fandom"
[6]: https://unofficialmageknighttheboardgame.fandom.com/wiki/Magical_Glade?utm_source=chatgpt.com "Unofficial Mage Knight - The Boardgame Wiki - Fandom"
