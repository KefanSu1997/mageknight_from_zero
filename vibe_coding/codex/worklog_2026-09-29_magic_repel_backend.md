# 魔法抗拒后台接入（2026-09-29）

## 原文与界限
已查看项目原英文技能图 `Assets/GameData/cards/skill_en_022.png` 第二行 Who Needs Magic? 及 `Assets/GameData/BattleDemo/Tactics/day_3.jpg`。

出版方旧官方FAQ链接 https://wizkidsgames.com/wp-content/uploads/mage/MK_FAQ_1.0v2.pdf 当前工具无法访问；官方FAQ全文转载 https://paperzz.com/doc/7255614/official-faq 可读。其规则明确：选择3后不能再使用源骰；三种横置替代技能不能叠在同一张牌；魔力窃取战术上保留的骰不算源骰，因为它已经离开Source。实现保留该正式战术例外并做实际保留骰付款验证。

有疑义组合未宣称官方澄清：魔力汲取/征引的改骰得标记、宣夺取骰，沿用项目既有WasUsedThisTurn语义算实际源骰取用；魔力风暴基础仅选源骰颜色得晶并立即重掷、强化仅重掷及给额度，不改成取骰历史。2014FAQ未细分这些操作语义，此分类仍需后续专门核准，不能把当前测试视为这些组合原规则已全部证明。普通/额外实际取骰、整回合禁令与真实横置主要入口不受此未决组合影响。

## 实现
- `SkillActions.PlaySideways` 追加 `bool forgoSourceDice=false`，007:07默认横置2；明确选择true才横置3，并检查本回合实际历史。其它技能不能使用该参数。
- 真实原牌必须在手牌，费用/类别/次数校验先行，正式ActionSystem完成横置后才标记技能次数及禁令。旧MagicRepelEffect无目标许可入口改为拒绝；不把全池或下一张未知牌改值。
- 玩家字段 `ForgoSourceDiceThisTurn` 使禁令跨ActionContext/战斗保留，正式本人回合结束/开始清理；使用历史复用ManaSource现有集合，没有另造一份历史。
- 所有普通/额外/转换/宣夺/legacy Take/TakeAny都在实际源骰层调用共享ValidateUse；CardManaActions也在修改额度前检查。ManaDraw/ManaPull及ManaClaim原效果validator在付款前检查禁令，防止花费已扣后失败。
- 保留战术骰不属于Source，仍允许；库存魔晶、已有标记、技能产魔力不会被禁令误伤。

## 验证状态
新增PveMagicRepelTests 23例；计划回归现有PveSidewaysSkillTests及直接受影响ManaDrawPullBasicStateTests、ManaClaimBasicStateTests。首次编译被并行正在编写的ShapeshiftActions缺using阻挡，尚未产生测试结果；已通知该代理，不修改其文件。


## 实际后台结果
- `AutomationOutputs/RulesState/20260929_042611_94aec17f/summary.json`：67/67 passed，0 failed/skip。
- 含23例魔法抗拒、既有横置替代技能、实际汲取/征引与宣夺直接受影响回归，以及并行变换身形11例；只在两个生产修改都准备完后统一编译一次。
- 不重复未受影响卡库。Unity编译与场景由主代理执行，此记录仅证明后台。
