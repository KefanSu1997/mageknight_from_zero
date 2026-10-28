# 勇者影响5及暴徒自然交涉的连续消费者计划

当前仅计划，尚未运行场景。`tools/build_pve_heroes_village_acceptance.py` 默认plan，--write/--from；111步骤172断言4关键图。不改生产/fixture/配置，不运行Unity/CLI。

## 原版与旧证据

原勇者共用能力是影响5，交涉中声誉+1；暴徒影响4、声誉-1。既有真实OriginalUnits报告074532中的暴徒unit_168步骤24..27已有原影响4→实际第二次招募及整次反转，详见`audit_2026-09-29_thugs_heroes_natural_interaction.md`，不重跑整套。

实际读取并复用 `AutomationOutputs/RulesState/20260923_021816_212ab687/results.xml` 中 `MK.Tests.Battles.PveHeroesUnitTests.ReputationRewardRequiresRealInteractionAndPreservesItsOriginalPriceModifier(True,1)` 与 `(True,-1)` Passed；`MK.Tests.Cards.ThugsRecruitmentTests.ActualThugsAbilityChangesReputationButNotFrozenModifier` Passed。PveHeroes的False,1、HeroesRecruitment12例也已通过，但仅作为已有范围，不据此宣称本路线验证翻倍招募。

## 连续路线

- Commoners红蓝各1、Lv6托瓦克真实三个指挥槽；移除原农民，保留暴徒槽0，选蓝勇者槽1，空槽2留真实招募。
- 五原牌行军000、承诺008、决心016、狂怒021、鲜血仪式advanced012；明确合法自定构筑而非标准16牌开局。
- 原行军移动2实际进入村庄0,1；鲜血仪式基础真实手伤1与红晶/绿标，只用于提供真实待治疗对象，不新增验收范围。
- 村庄正式开始交涉声誉0冻结；勇者影响5、声誉+1并耗竭；支付4实际招供应农民，余1，第三槽占满。
- 同次交涉暴徒原影响4/声誉-1，影响1→5、声誉1→0；实际买治疗3余2，再治疗同伤。结束回合清余影响/绿标、五原牌入弃牌。
- 正式新轮抽回五原牌并重整同三单位。下一战新招农民格挡2+原决心基础2实际挡4；同勇者普通攻击5实际击杀032，名望35→37。玩家及三单位跨轮同实例，原红2蓝1保留，手牌0弃牌5。

四图：勇者交涉原图及5影响；实际新招原农民；真实旧伤治疗；同勇者下轮击杀。运行后需用报告units.instances核对真实身份。

## 明确不覆盖

不把自有勇者影响能力当成招募勇者的声誉翻倍。四勇者精英供应来自真实核心板块揭示，当前后续地图来源独立缺口；不伪造核心板块或把勇者塞普通供应。无重测旧暴徒反转12例，也无四色同一共同能力四次重复。

## 实际执行结果

`20260929_170129_8822695c` 一次111操作172断言全部成功。四图已由主代理逐张查看：影响5/声誉1、实际农民招募付4后余1、真实旧伤治疗、正式下一轮同勇者击杀。最终Console两次0错误，见 `tmp/pve_heroes_village_final_console.json`。报告哈希、原单位跨轮身份观察见 `pve_heroes_village_acceptance_index.json`。无需生产修改、后台重跑或fixture种子变化。
