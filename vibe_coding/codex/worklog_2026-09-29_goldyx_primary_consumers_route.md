# Goldyx 荧光灵运、绿激励与一格飞行（后台通过，场景待验）

独立生成器 `tools/build_pve_goldyx_primary_consumers_acceptance.py`，默认仅计划，支持 `--write / --from N / --output`。当前68操作150断言5关键图；未写共享配置或 fixture，未运行CLI/Unity。

唯一新增后台组合方法：

`MK.Tests.Battles.PveGoldyxPrimaryConsumersJourneyTests.GreenInspirationOriginalDrawThenFreeFlightAndCrystalLuckHealActualBattleWound`

源文件 `Assets/Tests/RulesState/PveGoldyxPrimaryConsumersJourneyTests.cs`。它使用原 Goldyx16牌与 Random(0) 真正洗混，硬核对原开局和激励实际抽牌的引用；不往手里注入技能材料、伤牌或牌库顶。主代理后台通过后才写配置运行场景。

## 两个正式回合的同玩家路线

沿用39号HornGold已有种子0和红蓝各一晶，无预配部队。Goldyx等级6名望35、护甲3，恰好三个合法自身技能：005:05荧光灵运、005:06飞行、005:08激励。

第一回合原032实际攻击4造成两伤；原狂怒攻击2、耐力横置1击杀。保留原行进及真实伤牌到下一回合，名望37。

次回合激励实际抽另一张狂怒及原全神贯注006，得到**绿标**（不是绿晶）。行进强化支付该绿标得到移动4；刚抽到的全神贯注按原基础选绿晶，使真实库存为红蓝绿各1。先实际移动进森林花3剩1，再从森林(1,0)飞行一格到村庄(0,1)花0仍1，普通平原费2本来不足，因此一格免费分支有实际位置和费用消费者。

进入村庄正式交涉，荧光按三种真实库存产生影响3，实际购买治疗1并消除上一战真实伤牌一张；另一张保留。最终原16牌实体全部仍在，伤牌只余1，手6/库7/弃4、红蓝绿晶各1、同玩家两回合一战。

使用既有演练地图，不宣称新原板块探索完成；原牌名称/效果核对 `resources/text_json/basic_card.json`：006是**全神贯注**，000是行进，014是耐力。本路线没有用006强化或猜测输出。

## 复用证据与新组合门禁

- `PveSocialStormSkillsTests.CrystalLuckCountsInventoryColorsOnlyAndRealInteractionConsumesAndClearsInfluence`：实际晶体种类、交涉时机、治疗消费和结束清理；有效后台 `20260929_020955_20da8c83`，见 social_storm 索引。
- `PveRecoverySkillJourneyTests.InspirationDrawsSameOriginalInstanceThenPaysItsEnhancedEffect`（005/basic_card_000/Green/4）及 `PveRecoveryInspirationSkillsTests.InspirationDrawsActualCardsAndRewardsStrictlyLowestFame`（005/Green）：有效报告 `20260929_023014_2d4a1e11`、`20260929_022702_87753db7`。
- `PveHiddenFlightSkillTests.FlightActualDistancePaysOnlyPrintedCost`（1,0）：既有一格免费后台；`pve_map_skills_acceptance_index.json` 保留此前两格2费场景，不能拿它冒充本次一格已验。
- 本次只新增以上一个连续组合测试，证明实际原牌顺序、先移动再免费飞行、真实交涉与旧伤治疗的组合成立。没有理由全量重跑70技能或全部地图/飞行组合。

```powershell
& 'D:/study_and_work/unity_mcp_source_code/unity-mcp/Server/.venv/Scripts/python.exe' -X utf8 tools/build_pve_goldyx_primary_consumers_acceptance.py
# 后台成功后由主代理执行：
& 'D:/study_and_work/unity_mcp_source_code/unity-mcp/Server/.venv/Scripts/python.exe' -X utf8 tools/build_pve_goldyx_primary_consumers_acceptance.py --write
```

只读核对现有UI：全神贯注为 `Btn_OutsideChoice_green_crystal`；飞行从 `JourneySupport/Btn_UseOutsideSkill` 进入原地图选实际落点并 `Btn_Move`；荧光通过原 `Btn_BeginInteraction` 后的自身技能入口。未增加生产UI。

## 后台门禁及生成器冻结

已实际读取 `AutomationOutputs/RulesState/20260929_183304_2f681967/summary.json`：总2/2（Goldyx本方法1项、独立Arythea1项），无失败。不能把这写成Goldyx新增2项。原牌次序、免费飞行和治疗连续组合硬断言全部通过。

`tmp/pve_social_assault_compile_console.json` 已读取：两次success且data为空，Unity编译双0。

只读复核实际UI：`JourneyHand` 动态生成 `Btn_OutsideChoice_green_crystal`，飞行打开地图时关闭手牌页，`Btn_Move` 真正调用 `Fly` 后只清飞行指针而保留所选技能供次数观测；`Btn_CloseMap` 在本路线无卡选择时返回旅程。交涉/购买治疗/治疗伤牌使用现有按钮。生成器全部断言key均在当前Pve控制器中存在，技能顺序5/6/8与构筑排序一致。

生成器冻结为68操作150断言5图，未作动作/配置/Assets更改，待主代理串行运行。后台通过不代替场景结果。

## 首轮场景失败及最小补验

`AutomationOutputs/PveBattle/20260929_183703_9e33b41e/report.json` 第37步失败：原手牌7张，006在第二页，`@card`只查当前可见页。规则/费用未失败，0–36保留有效；原失败报告不改。生成器仅在旧37之前增加 `JourneyHand/Btn_NextHand`，不改生产或断言，后续以 `--from 37` 仅补新翻页和失败后缀。当前69操作151断言5图；前缀截图不重复。

## 补验成功，最终Console待统一收尾

`AutomationOutputs/PveBattle/20260929_184109_56c849b2/report.json` success，共69步；仅37–68的32步78断言计新增，0–36纯重放不计新增。合旧前缀37步73断言，本路线69有效操作151断言。旧32图及新40/51/59/68四图由主代理全部目视正常。

实际状态已核：40步006原全神贯注进入已打出区并建立绿晶，红蓝绿三种库存、绿token0；44步森林消费后剩移动1；51步免费飞行到0,1仍1，飞行次数True；59步荧光影响3，三个自身技能次数均True；63步支付影响3得治疗1；66步上一战真实伤2→1；68步同一Goldyx两回合手6/库7/弃4、原16牌加伤1。

索引 `pve_goldyx_primary_consumers_acceptance_index.json` 已保存原失败与有效分段、哈希、原图路径及实际消费者。最终Console暂pending，等本批全部场景完成后统一补 `tmp/pve_social_assault_final_console.json`；编译双0与最终检查分开记录。英雄账本005:05/06/08已补入本次证据，未改生成器或生产。

## 最终Console已完成

本批四场景结束后，实际读取 `tmp/pve_social_assault_final_console.json`：两次 status=success、result.success=true、data=[]，确认两次0错误。对应本路线索引 finalConsole 已由 pending 更新为 passed 并记录文件哈希；前述 pending 为过程记录，不代表当前仍待检查。未改英雄账本或共享最小差集。
