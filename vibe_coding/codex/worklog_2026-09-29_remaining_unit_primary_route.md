# 巫师免费远程与斥候攻城/移动：连续场景准备

状态：仅生成路线计划，**未运行场景，未声称通过**。新增独立 `tools/build_pve_remaining_unit_primary_acceptance.py`；未改生产、场景资产、fixture、共享配置。默认 plan only，支持 `--write`、`--output`、`--from N`。本次只调用 Python 默认计划，92操作、141断言、3关键截图；没有运行 RulesCli/Unity。

## 合法起点及资源

复用 `build_pve_shock_forester_acceptance.py` 已验的 Commoners 起点UI/五原牌选择前缀，仅读取其 build() 返回值，无写入副作用；HeroBuild 改选原托瓦克等级4，名望15、两槽、无技能。真实移除旧农民/暴徒，选入巫师 army_014 与斥候 army_2_009；不注入单位、免费新增资源或修改图/敌。

五张既有原牌为 basic_card_000、008、011、016、021。沿用既有起点库存红/蓝各1。属于已招募单位合法自定构筑，**不是标准16牌开局，也不冒充自然招募**。位置起始0,0；使用既有 authored 地图0,1平原村庄与两个原敌遭遇，不宣称原新板块探索。

0–24仅操作起点UI，跳过重复截图/断言；25步检查等级4、名望15、手5、单位2、红蓝各1、同玩家。新路线不是既有已过前缀报告，不能把准备步骤虚报为新消费者验收。

## 两回合一次正式跨轮

| 步骤 | 操作及实际消费 | 预期 |
|---|---|---|
| 28–37 | OwnedUnits选斥候 index1、能力2，移动2；实际地图支付平原2到0,1 | 移动2→0，斥候耗竭；截图37。只验移动，不把探索许可当两格探索完成 |
| 39–56 | 五牌标记正式末回合弃牌；遭遇0原monster_032甲3；巫师 index0 能力0免费远程3 | 实际远程3单独击杀甲3，名望15→17，无伤，无魔力支出；截图56 |
| 57–64 | 正式剩余战斗阶段/结束回合；实际空库宣布轮末、EndRound、PrepareRound | 手牌0弃5→真实洗回5并抽5，同两单位重整，位置0,1与库存红蓝各1保留；不重建玩家 |
| 65–86 | 正式夜战术/第二回合；四未用原牌标末回合弃；遭遇4原monster_007狼骑兵甲4；同斥候能力0攻城1，真实利斧投掷强化付蓝1得远程3 | 攻城1+远程3恰好满足甲4，缺一不可；蓝1→0，同斥候耗竭；击杀名望17+3+原强化利斧奖励1=21，声誉仍0；截图86 |
| 87–91 | 正式战斗结束和回合末弃牌 | 两回合/两战，手0弃5，红1蓝0，无伤；同巫师就绪、同斥候耗竭，位置仍0,1 |

原利斧强化的执行实现 `ThrowingAxeEffect` 是击杀奖励**名望**1；不得将本地中文资产 OCR 的“声誉”当实际规则预期。本路线未修改该原卡或扩大其测试范围。

## 后台复用而非另写模拟

- `20260923_021816_212ab687/results.xml`：`PveIllusionistSorcererTests.OriginalSorcererModesPayTheirOwnFeesAndFeedRealRangedAttack(0,"monster_032",2)` Passed。
- `20260923_030209_f193d1c4/results.xml`：`ScoutsStateTests.PrintedSiegeAddsOneToActualOriginalRangedAttack` Passed，原源码是斥候1+弩手2合并贡献实际击杀。该批其他失败仍保留；不把整批写成全绿。
- `20260923_100318_20787bba/results.xml`：`PveJourneyOutsideUnitTests.OriginalMovementEntersSharedPoolAndMovesSamePlayer("army_2_009",2,3)` Passed。
- 跨轮沿用已通过正式旅程机制。第二战换用原利斧3补足甲4，使用同一个正式攻击贡献合并消费路径；此组合属于本场景待验，不声称对应精确后台新路线已运行。无生产修改及新规则，当前不新增后台重复测试。

## 主代理串行执行入口

```powershell
& 'D:/study_and_work/unity_mcp_source_code/unity-mcp/Server/.venv/Scripts/python.exe' -X utf8 tools/build_pve_remaining_unit_primary_acceptance.py
& 'D:/study_and_work/unity_mcp_source_code/unity-mcp/Server/.venv/Scripts/python.exe' -X utf8 tools/build_pve_remaining_unit_primary_acceptance.py --write
```

写配置后由既有 Unity 场景自动化入口运行；本生成器不启动编辑器。若中途失败，修正后用 `--from N --write` 只保留失败点以后断言/截图，之前完整合法操作仍重放。实际执行后核三截图及 `units.instances` 在25/61/64/91一致，回传真实结果与 Console 门禁；现阶段台账仍未升级场景状态。


## 实际场景结果

`20260929_181557_d1629d33`一次 success，92总操作、141断言。0–18共19起点无断言重放不计新增；19–24共6布局检查；25–91为67路线操作。共73带断言操作，不能把准备UI算作能力消费者。37/56/86三图由主代理逐图目视正常。

报告25/61/64/91的 `units.instances` 均为 `army_014:-1030767852,army_2_009:-491793980`，四处 `journey.samePlayer=True`。第一战双耗竭、正式轮后双就绪、第二战仅斥候耗竭；真实最终名望21、红1蓝0、无伤、两回合两战及位置0,1。三项场景已通过，已更新主部队账本；其余缺口保留。索引 `pve_remaining_unit_primary_acceptance_index.json`。

最终 Console 已由主代理检查，`tmp/pve_primary_delta_final_console.json` 两次均0错误；部队场景完成后的独立双0记录也见 `tmp/pve_remaining_unit_primary_console.json`。
