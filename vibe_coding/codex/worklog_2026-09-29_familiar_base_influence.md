# 非白魔宠基础影响3与迷醉林地4连续实际消费者（2026-09-29）

## 计划与范围

本批仅补 `army_2_004` 非白调谐基础影响3，合并布拉瓦尔原迷醉 `hero_skill_pool_001:04` 的实际林地影响4。不是未调谐魔宠；红调谐来自本局实际招募付款，始终保留同一玩家/单位/伤牌。既有红攻5、蓝挡7、白移动或影响5、绿治疗3不重测。

- 独立后台：`Assets/Tests/RulesState/PveFamiliarBaseInfluenceJourneyTests.cs`，唯一方法 `OriginalGladeTranceRecruitsRedFamiliarThenBaseInfluenceHealsRealPreviousBattleWound`。
- 独立生成器：`tools/build_pve_familiar_base_influence_acceptance.py`，默认仅plan；`--write`写指定输出，`--from N`重放前缀而跳过其断言和截图，`--output`可选。
- 默认plan：128操作，120断言，4关键截图；前63操作为既有原地图移动/自然供应轮换重放，不计本批新增验收。新增65操作。
- 后台/Unity/场景尚待主代理串行执行，本文不声称通过。

## 真实连续路线与资源

1. 复用既有HornGold起点（红蓝库存晶各1、无单位）；原地图A/1/2，布拉瓦尔等级6名望35、护甲3、手限6，仅学原迷醉。五原牌000行进、008承诺、014耐力、advanced018外交、advanced039山川逸闻。
2. 原行进2+耐力2+山川逸闻3=移动7，实际移动(0,-1)、(1,-2)、(1,-3)费用2/2/3，到原林地。正式结束回合。原空库轮末声明、正式准备与昼夜战术轮换至R5，供给自然出现魔宠。
3. 林地当回合迷醉4，承诺基础2，总影响6；实际R5供应原魔宠，红晶1支付调谐，影响6→0、红晶1→0、蓝晶1保留。原林地金标记未转换/未冒作费用。正式结束招募回合。
4. 同轮下回合仍在林地，真实挑战(1,-4)原032。魔宠不发动、不耗竭，原英雄实际承受攻击4取得2伤。耐力/外交/山川逸闻各横置1，近战3击杀真实甲3哨兵，名望35→37，声誉0→1，真实地图标记移除。原林地回合末去除一张战伤，另一张实际旧伤保留。
5. 下回合行进000仍在手：招募仅打008、战斗仅打014/018/039，没有弃000或声称空库补牌。用真实行进2移动至(2,-3)村庄，魔宠同实体且仍就绪。原能力索引2/选项1给影响3（红非白，不加2）并耗竭。声誉1修正0；村庄正式交涉花3影响购买治疗1，真实治疗上一战保留的同一伤牌；正式结束第四回合。

## 源码/UI定向核对

- `TranceEffect`依据实际MovementMap.Place识别MagicalGlade给4，不覆盖地图/位置。
- `PveJourneySession.Interaction`正式林地招募以供应引用、影响6及色费检查；村庄治疗每点3影响。
- `PveJourneySession.Units.UseOutsideUnit(familiar,2,option:1)`与UI `OwnedUnits/Btn_Ability_2`、`Btn_Option_1`对应原移动/影响二选一。
- 原技能UI为 `JourneySupport/Btn_OutsideSkill_0`、`Btn_OutsideOption_0`、`Btn_UseOutsideSkill`；只有已学迷醉，0不是原技能池下标。构筑中下标4选原迷醉。
- 部队色枚举明确采用 `ManaColor.Red`、`Btn_RecruitMana_Red`；无数字枚举假设。
- 战斗默认手牌界面，无多余Roster切换；直接选三原牌横置。
- 实际观察字段 `hero.id`、`hero.skills`、`skills.used`、`units.attunements`、`units.ready`、`journey.samePlayer`、`journey.position`、`influence/healing/wounds` 均复用现有正式观察。
- 后台读取现有scene的原unitCatalog与worldEnemyCatalog顺序，加载既有原unit定义/monster定义与tutorial_opening.json。无修改seed/fixture，未人为设Offer、地图格、ready、调谐或伤牌。

## 执行记录

仅执行生成器默认plan检查，未写共享config。测试文件交主代理串行后台先验；未运行RulesCli/Unity。源代码定向核对已把治疗观察改为正式 `Context.Healing.Total`，旧伤身份断言采用ReferenceEquals，英雄Armor3与实际等级6场景一致。保留真实后台失败后定向修复，不把计划记作通过。

## 后台与编译已验证（场景尚待结果）

- `AutomationOutputs/RulesState/20260929_195848_27e5a6fe/results.xml`实际唯一方法Passed，total1/passed1/failed0，37条断言。XML SHA256：`a0266f1ad37b695bf866ebf5d2c6365af677baf0c95bdc3138f7b583c809c89a`。该测试真实连续牌序与次回合原行进来源已通过，无重跑全库。
- `tmp/pve_glade_insight_compile_console.json`两次success，data均空、message均Retrieved 0 log entries；本批编译Console双0。SHA256：`8d62f369865d478d110ffb57efc54be5aff20c4f4af96e654eba68ad28fb3864`。
- 生成器计数再次以默认plan核对：128总操作=前63纯重放+65本批新增操作；前缀0-based0..62无断言/截图，新增检查从step63开始，120断言、4图。没有以128作为新增。
- 独立索引 `pve_familiar_base_influence_acceptance_index.json` 已创建。主代理场景`20260929_200051_5d478abe`仍运行，暂不记场景通过；最终Console另待场景结束核对。

## 实际连续场景通过

`AutomationOutputs/PveBattle/20260929_200051_5d478abe/report.json`实际success，128/128操作、120/120断言通过；仍按63旧前缀重放+65新增记账。主代理已逐图目视67/99/115/125四图通过，原迷醉卡面/红调谐魔宠、基础影响3、旧伤消费数值正常，无截断。

关键step采用报告0-based索引：
- 67迷醉实际林地影响4；78招募影响6→0、红晶1→0、蓝晶1保留，真实红标。
- 78/80/81/99/100/115/122/125/127的 `units.instances` 均为 `army_2_004:748256168`，全部`journey.samePlayer=True`。同单位没有重新构筑或免费重整。
- 99林地结算后旧伤1、手牌`basic_card_000,w`且魔宠ready=True；100正式下一回合同手牌。这证明行进保留，不是假补牌或凭空取得。
- 115实际村庄(2,-3)，红标魔宠影响3、ready=False，伤1；122交涉花3影响换治疗1，125真实治疗1→0、旧伤1→0、手牌清空。
- 127正式末回合completedTurns4/completedBattles1，红晶0蓝晶1，魔宠仍红标且耗竭、同玩家，原伤0。
- 场景报告没有独立伤牌instance字段，因此场景记录连续牌区与数值；后台已经以ReferenceEquals验证保留/移除同一旧伤，不将场景缺字段夸大为观测到了唯一伤牌哈希。

独立索引已更新为scene_passed；最终场景后Console仍pending，由主代理统一核查。未修改共享ledger或生产代码。

## 场景结束后最终Console

已读取 `tmp/pve_glade_insight_final_console.json`，两次均success且result.success=true、data为空，实际双0。独立索引finalConsole由pending更新为passed，保留文件SHA256。此前pending为当时状态；本批后台、场景、逐图目视与最终Console现已完整闭合。仅更新本批索引和工作记录。
