# Wolfhawk 三技能主要消费者连续路线

状态：独立路线及后台测试已准备；尚未执行后台或场景，不记为通过。默认计划输出为 58 操作、112 场景断言、5 张关键截图。没有写共享配置、修改生产规则或运行 Unity。

## 本批有限范围

| 原自身技能 | 实际消费者 |
|---|---|
| hero_skill_pool_004:08 激励 | 原 16 牌正式洗混后实际抽 2；本人名望 35→36；新抽的另一张狂怒保留跨回合，第二战实际打出攻击 2。奖励是名望，不是魔力。 |
| hero_skill_pool_004:05 洞悉猎物 | 选已有演练原寒冰巫妖 monster_004，选项 115 只忽略物理抗性，仍保留冰抗及原定义；付库存红晶用原狂怒强化物理 4 击杀原护甲 4。未移除物抗时该贡献只有 2。 |
| hero_skill_pool_004:06 嘲讽 | 第二正式回合选原兽人骷髅战士 monster_032 真实攻击槽，降攻分支 4→3；英雄护甲 3 实际只增加 1 张伤牌；随后原攻击 2 与行进横置 1 击杀同敌。 |

合法 Lv6、护甲3、三个本人技能；复用已存在 HornGold 种子 0 构筑的红蓝各一晶、无部队，再选本人原16牌。采用演练中原敌，不冒充原七格地图的征服或守军来源。第一战冰攻2造成1张实际伤；跨正式回合保留该伤、新抽原牌及剩余库存。最终同玩家两战两回合，名望43，手7/库8/弃3，共原16牌及两张真实战伤；红晶0、蓝晶1。

## 执行入口

- 生成器：`tools/build_pve_wolfhawk_primary_consumers_acceptance.py`。默认只输出计划，后台通过后由主代理 `--write` 写配置；失败可 `--from N` 重放有效前缀并只记录后缀。
- 新后台组合：`MK.Tests.Battles.PveWolfhawkPrimaryConsumersJourneyTests.FameInspirationDrawThenInsightPhysicalResistanceAndTauntActualDamage`。
- 独立测试：`Assets/Tests/RulesState/PveWolfhawkPrimaryConsumersJourneyTests.cs`。使用真实原16牌、原怪物、正式 Journey/战斗入口，不注入伤牌或伪造新抽牌。以引用检查激励抽入的牌、跨回合旧伤和全部原牌保留。
- 场景关键图：名望奖励/抽牌、洞悉选定能力、红费实际击杀、降攻后的真实新增伤、第二正式回合结束。

## 精确复用依据

- `Assets/Tests/RulesState/PveInsightPreyTests.cs::ActualResistanceConsumerAndIdentity` 已验证物抗/元素抗性实际贡献及目标身份；索引 `pve_flip_insight_acceptance_index.json` 保存后台与旧迅捷场景。本批补防御物抗主要消费者，不重测全能力排列。
- `Assets/Tests/RulesState/PveHeroAttackSkillsTests.cs::CurseAndTauntChangeOnlySelectedActualSlot("004",6,0,-1)` 已验证原嘲讽降攻；索引 `pve_enemy_attack_skills_acceptance_index.json` 保留旧有效后台与增攻减甲场景。本批不重复增攻减甲分支。
- `PveRecoveryInspirationSkillsTests.FameInspirationPaysActualLevelThreshold` 与 `TiedOrHigherFameDoesNotReceiveBonus` 为名望奖励来源；`pve_recovery_inspiration_acceptance_index.json` 保存有效后台。共享抽牌实现不等于本英雄本次已场景通过，本批补本人抽牌后实际消费。
- `Assets/Logic/Runtime/CardEffects/InsightPreyEffect.cs` 原能力选项公式 `100+(int)Ability.PhysicalResist`；原枚举值15，故真实按钮 `Inspector/Btn_SkillChoice_115`。
- `resources/text_json/monster.json` 核对004物抗/冰抗、甲4、冰攻2、名望5；032甲3、物攻4、名望2。原狂怒 `basic_card_021` 与行进 `basic_card_000` 使用既有效果；第二张激励新抽牌 `basic_card_002` 原名凝结，本批不消费它。

## 待验记录

新连续组合后台、Unity 编译双零、场景真实报告及截图目视均待主代理执行。无依据将准备状态标记为通过；未改英雄台账或最小差集。

主代理首轮后台 `AutomationOutputs/RulesState/20260929_190137_8cc30dbd/results.xml` 失败1项：测试第78行错误地期望洞悉猎物在下回合恢复。原 `resources/text_json/skill.json` 明确每轮一次；`SkillActions.StartTurn` 仅清回合次数，`StartRound` 才清轮次数。已修测试为原频率 OncePerRound 且第二回合仍已用，并修对应场景 `skills.used=True,True,True`。未改规则、动作或绕过次数，原失败保留，待只补这一方法后台。

复验已核实：AutomationOutputs/RulesState/20260929_190314_453b101e/results.xml，本方法 1/1 通过、43 断言，原16牌顺序、实际伤、抽牌引用、名望与付费组合均通过。独立 pve_wolfhawk_primary_consumers_acceptance_index.json 已记录通过/失败报告 SHA256。当前仅后台通过，场景及最终 Console 仍待主代理，未改 C#。

场景首次 20260929_190823_4f5c6d5e 第42步 Skills/Btn_Skill_1 不可见（Button not found）；原0–41有效42操作/74 断言，18/26/35三图由主代理目视通过。已保留该失败和有效前缀于独立索引。主代理仅在42前加入既有 Btn_NextSkill，再 --from42 重放前缀补后缀，结果待定。此次与此前Goldyx手牌分页同类：脚本必须按实际可见页选按钮，不能仅按合法持有序号假定可见。本人未改生成器、生产或ledger。

后缀已核验：20260929_191109_d43b4195 success，59总操作，其中0–41只重放，42–58新增17操作/39断言；合并旧有效前缀得59有效操作、113断言。新49/58两图及旧18/26/35共5关键图主代理已目视通过。最终实际Wolfhawk、3本人技能、samePlayer=True、2回合/2战、名望43、真实伤2、手7/库8/弃3=总18、红0蓝1；洞悉每轮次数仍已用。原抽入狂怒实际打出和嘲讽4→3后仅新增1伤均按有效后缀保留。独立索引已更新为scene_passed_final_console_pending，最终Console仍待统一；不改ledger。


最终补记：主代理已取得 	mp/pve_special_primary_final_console.json 实际两次零错误；对应独立索引finalConsole已更新，当前无运行中场景。
