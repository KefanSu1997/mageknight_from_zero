# 原地下城与忠诚部队真实消费者后台

依据原图 `Assets/GameData/cards/place_008.png` 与只读审计 `tmp/loyalty_dungeon_consumers_20260929.md`。不联网、不操作Unity、不推送。

## 实现
- `PveJourneySession.EnterDungeon(Random rewardRandom=null)`要求玩家实际所处Map.Places格为Dungeon且本回合仍有行动，从持续EnemyTokenSupply抽一枚实际棕敌，原玩家/原Context进入正式四阶段战斗。明确跳过普通战斗的历史击败拦截，仅此新供给遭遇允许回洗的实际标记重用；不清空原历史。
- `DungeonActions`保存实际地点、敌人、供应、进入前昼夜与先前征服状态。战斗临时Context.DayPart=Night、UnitsAllowed=false，不改全局Game.Clock。主代理接入共享单位参与门，实际激活/伤害结算只对忠诚绑定实体豁免。
- 由正式UnitDamageResolver.EndBattle钩子结算，无论胜败将同一敌人标记弃回供应，恢复外部昼夜和部队许可。首胜更新原地点盾徽，正式独立六面ManaDie决定奖励；已征服重游只有原击杀名望，不改盾徽、不重发奖励。
- 金/黑奖励复用既有真实法术供应选择与PlayerTurnEngine回合末入牌链。其它骰面用`RevealDungeonArtifactReward()`翻出真实两张；`SelectDungeonArtifactReward(actualArtifact)`选实际引用，回合末选中入牌库顶、另一张归底。不足两张时揭示拒绝，不能先抽一张。奖励未选或选择无效时回合结束前拒绝，不回滚整状态。GrantReward为internal，仅正式回合入口调用。
- 元数据发现：正式两份monster JSON没有任何Brown。核原图 `monster (66).jpg` 九头蛇为棕底、护甲6冰抗、三次物理2、名望5，与已有数值一致，只补两份JSON的monster_066 token_color=Brown。不为该路径伪造敌色，不顺带改未知整库。

## 后台证据
- `20260929_060348_35cedcec`：新 `PveDungeonJourneyTests` 首批9/9。覆盖实际日间进入后夜间黑骰+原强火球8攻城火焰击杀、金/黑/红三奖励分支、首胜原盾徽、奖励延迟原牌入牌、失败后弃敌再抽同一物理标记、已征服仅名望重游、无地点/已用行动/休息3拒绝，以及真实契约招募农民对普通农民的激活与攻击槽承伤区别。
- `20260929_060501_5de17661`：只补新神器不足/实际二选未选归底1例；共享PlayerTurnEngine入口直接回归 `TunnelTravelStateTests.TowerSpellIsSelectedFromRealOfferAndOnlyGrantedAtEndTurn`2及`StaleRewardSelectionRejectsBeforeAnyEndTurnMutation`1，4/4。
- 累计新地下城10/10，直接回归3/3。源码测试冻结，CLI已释放。Unity Console与真实场景由主代理继续；这些后台证据不能替代场景验收。

## 范围与限制
- 只接原place_008地下城，不把墓穴或其它冒险地点标签套在普通战斗上。
- 当前仅核对并加入路线的monster_066棕色元数据，未声称全部棕敌目录已校对。完整随机棕敌供应覆盖需以后增量核原图。
- 奖励供应不足时明确保留待选/未完成状态，不造奖励补足。单人连续旅程已后台覆盖；多人同时领取资源没有本批场景证据。
