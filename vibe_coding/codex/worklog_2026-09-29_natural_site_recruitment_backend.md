# 实际地点共用交涉招募后台（待运行）

2026-09-29。按 `natural_site_recruitment_audit_2026-09-29.md` 的已核原图/规则实现。子代理未运行 CLI/Unity；主代理负责串行测试和场景。此文不代表通过。

## 实现

- 新 `PveJourneySession.SiteInteraction.cs` 从实际玩家坐标、已放置地图和同 PlaceState 获取合法地点：村庄、本人已征服要塞、任意已征服法师塔/城市。敌人、剩余守军或 GuardNeedsReplacement 使城防地点不能交涉；修道院本轮明确未接（缺真实焚毁生命周期）。
- 新 session API：`CanInteractAtCurrentSite`、`CurrentSiteRecruitLocation`、`CurrentInteractionSite`、`CanRecruitSiteIcon(UnitCard)`、`BeginSiteInteraction()`、`RecruitSiteUnit(UnitCard,ManaColor? familiarMana=null)`、`EndSiteInteraction()`、`DismissSiteUnit(UnitState)`。
- `ActionContext.InteractionSite/InteractionPosition` 绑定交涉开始的实际地点；每次招募/结束/解散重新校验坐标与引用，拒绝地点漂移或替换。同类型假副本不等同当前地点。正式 ClearInteraction（EndTurn）清空绑定。
- `InteractionCityInfluence` 独立记录本人城市盾徽数。Begin 原声誉门槛成功后加一次，不进入声誉翻倍/反转/忽略的记账字段，不能重复开始刷取。
- 白城任意原招募图标由 `RecruitmentService.AllowsWhiteCityRecruitment` 检查实际绑定地图/地点及已征服白城；只有图标放宽。仍沿 `CardUnitActions.TryRecruit` → 原 RecruitmentService 处理印刷费用、真实供应、额外魔宠基本色魔力、指挥槽和原奖励。不能借用 RefugeeCamp 因为那会产生非白城加价。
- 原村庄 Begin/Recruit/End、治疗和 DismissUnit 保留严格村庄 wrapper；内部共用实际地点校验。旧直接服务的无地图 backend fixture 仍可走无绑定路径，不能因此获得白城许可。旧自造交涉类未调用。

## 新测试

`PveNaturalSiteRecruitmentTests` 共21项：四地点真实招募并同斥候下一回合正式攻城1+原愤怒攻击2击杀；六类敌对/他人/缺生命周期地点拒绝；两色城市图标与原价、刚招农民实际影响消费；四组正负声誉勇者/暴徒与盾徽分离、两次真实招募；奥术伪装只除声誉罚值；两类坐标/地点实例漂移；白城魔宠真实颜色额外付款；严格村庄兼容。

有限回归建议：JourneyInteractionTests（村庄wrapper）；HeroesRecruitmentTests 的 TwoHeroesDoubleFrozenWholeInteractionModifierOnlyOnce 与 HeroesAndThugsCannotBothBeRecruitedInOneInteraction；ThugsRecruitmentTests 的 TwoThugsReverseOnceAndEndTurnClearsState 与 FailedRecruitCannotReverseOrSpendAnything 与 RefugeeCampUsesPrintedLocationInsteadOfEliteFlag；PveGladeRecruitmentTests（原林地路径）。不跑全库。

## 保留缺口

没有实现修道院焚毁/招募、各色城市额外购牌/神器或补精锐供应、法师塔影响购法术；地点招募初版通过不能替代整地点规则通过。场景还须真实连续操作、原图/数值观察及 Console 两次零错误。

## 实际结果

后台 `20260929_171747_fa03220e` 新21及直接受影响旧25共46/46通过。Unity编译后Console双0；domain reload期间一次MCP连接帧中断，等待并refresh(wait_for_ready)恢复，未重复请求编译或重启场景。场景 `20260929_172048_1890cc47` 一次80操作157断言通过，四关键图已目视。正式征服塔/回合末领奖→次回合金币焚毁9减原声誉1后8、付7招原魔像→下一战同魔像攻击2与保留奖励牌横置1击杀；Lv6护甲3真实两伤保留。最终Console双0见 `tmp/pve_site_recruitment_final_console.json`。详细报告哈希与单位身份索引 `pve_natural_site_recruitment_acceptance_index.json`。

当前已接地点接口含要塞/塔/城市，后台已过；本批真实场景只证明塔路线，不能扩写全部地点场景已通过。没有修改原卡/fixture/种子或推送远端。
