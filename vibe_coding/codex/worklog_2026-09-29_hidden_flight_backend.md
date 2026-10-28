# 隐秘之路与飞行后台（2026-09-29）

## 原规则与实现
- 隐秘之路001:07仍复用正式SkillActions.Use，每回合一次。option0移动1、山脉费用5且安全；option1先预览并实际支付一点蓝魔力，才增加移动1/地形许可、湖泊费用2且安全。若其它效果曾要求入湖逐次付蓝，本技能已付蓝后直接适用其原湖泊费用，不再次扣蓝。
- 非法选项、战斗内、休息/行动已完成、待重复效果、蓝费不足都在资源变更前拒绝；失败不标技能使用。
- 飞行005:06新增FlightSkillActions.Move(p,context,skill,target,others=null)。同一真实玩家自有未用技能，按真实轴向距离1免费或2扣2移动，成功才写轮次数。MovementService内部TryFlightUsingPool复用CardTravelActions.IsSafeDestination，更新真实Position/CurrentTerrain/MovedThisTurn及SiteVisibility；不经过中间格，不调用肆虐激怒。
- 飞行没有设置TemporalPortalPending、TeleportRange或全回合IgnoreRampaging。下一次普通移动仍走原RampagingEncounter，不能把技能一次豁免变成整回合豁免。旧无目标FlightEffect拒绝。
- 两技能直接复用现有地形许可及正式回合/轮次生命周期，不新增Context临时许可字段。

## 验证
新增PveHiddenFlightSkillTests 12例：蓝费两分支真实原行进消费/移动、费用失败守恒、飞行1/2格、实际轮次数跨回合保留、安全/占用/行动/移动不足拒绝、飞行后普通移动真正激怒同一敌人、旧空目标入口拒绝。
既有MountainLoreBasicStateTests.HiddenPathMountainAndLakeSafetyExpireAtEndTurnAndDoNotReturnNextTurn补实际蓝费，因为它原先直接免费执行option1；保留全部原安全与过期断言。
待与探索/揭示代理合批CLI，未操作Unity。主代理负责Pve/UI及场景。


## 合批后台结果
`AutomationOutputs/RulesState/20260929_044431_0eb5e1e0`：29/30，唯一失败为并行野性盟友新增伤害消费者预期，已交负责代理修复。隐秘/飞行12例及既有隐秘安全生命周期1例全部通过；不能将整批记全绿。其它已过12例探索/揭示和4直接回归无需重复。
