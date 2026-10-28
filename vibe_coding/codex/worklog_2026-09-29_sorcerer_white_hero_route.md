# 巫师双模式与白勇者连续路线计划

状态：生成器就绪，未运行Unity，不能记为场景通过。

- `tools/build_pve_sorcerer_white_hero_acceptance.py`：默认仅计划；`--write` 写验收配置；`--from N` 仅补失败后缀，成功前缀重放不重复证据。115操作、173断言、4张关键截图。
- 合法等级6托瓦克、三个真实指挥槽，原巫师/白勇者/幻术师；无英雄技能。五张真实自定手牌，不冒充标准16牌开局。复用Commoners初始红蓝各1晶，不修改任何fixture或种子。
- 第一战：幻术师产生白晶，巫师白费去巨鹰原城防并远程3，原利斧远程1追加，真实4击杀甲4。其余原牌正式回合末弃牌。
- 同五牌正式换轮、同单位重整；第二战鲜血仪式基础新增真实手伤/红晶/绿标，巫师绿费去冰巫妖实际抗性、利斧追加远程4击杀。直接核对 resources/text_json 原数据：冰巫妖同时具有寒冰和物理抗性；移除后普通远程4不再减半，真实击杀护甲4。
- 同轮第三回合仍同玩家：幻术师白晶支付白勇者远程7，真实击杀原032，伤牌与资源/同单位耗竭保留。未重建快照。

## 后台证据已读

`AutomationOutputs/RulesState/20260923_021816_212ab687/results.xml`：三项 OriginalSorcererModesPayTheirOwnFeesAndFeedRealRangedAttack(0/1/2) 与 WhiteRangedSevenPaysWhiteAndKillsBeforeEnemyAttack 均Passed。绿去抗性模式的目标004与原数据一致。同期 RedColdFireAttackConsumesRedAndDefeatsOriginalIceResistantEnemy、GreenHealingPaysForActualLevelThreeUnitAndOneHandWoundWithoutReadying亦Passed，可供另一路线复用。

## 剩余

红勇者、绿勇者另建连续路线，避免四五部队塞入三槽。主代理需运行本场景、检查4截图以及同实体观察，再记录最终通过，生成器本身不构成场景证据。

## 首次运行与显示补验
`20260929_161107_a5453657` 一次115操作173断言数值全部通过，四图已目视，但敌人Numbers文本仍显示原城防/抗性，无效果移除提示。不能仅凭数值就算显示合格。主代理修改PveBattleController.Abilities按实际敌索引读取正式FortificationsRemoved/ResistancesRemoved（及已有单火抗移除），显示“已移除”，原标记图片不变，洞察保留“已忽略”。纯显示修改，不变规则。只补50与80画面及截断检查，旧规则消费者不重验。

显示补验 `20260929_161557_6dc68287`：50城防说明通过；80双抗性“已移除”导致Numbers截断。保留失败，调整已移除能力敌人的Numbers为三行紧凑布局（攻击/护甲与名望/能力状态），不缩掉规则文字。涉及两处显示都需重新确认，原115/173规则结果保持。

最终显示补验 `20260929_161913_7c74e4b0` 仅50/80两断言两截图通过，目视“城防已移除”“冰抗已移除·物抗已移除”清晰且无截断。原115/173连续消费者不重复统计；七处同三部队instances完全一致，索引pve_sorcerer_white_hero_acceptance_index.json。原图保留印刷图标，当前效果写在其下，明确区分原卡与战斗状态。
