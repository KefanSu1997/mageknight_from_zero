# 奇袭队前两能力与护林人格挡连续路线

## 当前状态

计划现已取得实际场景通过证据，详见末尾验收记录。生成器默认 plan，`--write` 写指定配置，`--from N` 保留实际前缀操作但不重复断言/截图。未修改生产、配置或fixture，未执行CLI/Unity。

## 原版依据与旧证据

已实际查看 `Assets/GameData/cards/army_2_010.png`、`army_2_012.png`、`army_2_000.png`：奇袭远程1且本回合其他部队全部攻击+1，第二能力减单敌护甲/一次攻击各1；弩手远程2；护林人格挡3。三者对应BattleDemo/Units资产继续绑定原图/原JSON。

实际读取 `AutomationOutputs/RulesState/20260923_023606_b1e4750f/results.xml`：RangedBuffUsesActualOtherUnitContributionsBeforeOrAfterActivation(True/False) 两例及 WeakenedArmorAndAttackFeedActualBlockAndMeleeConsumers 全部Passed。正式相同后台已证明部队/卡来源、减攻减甲及护林人格挡消费者，无生产改变不重复跑。

实际读取旧 `AutomationOutputs/PveBattle/20260923_024535_dcde4ea8/report.json`：13步旧场景仅奇袭第三能力。护林人原移动另有已通过连续证据；不重复这两项。

## 合并路线

`tools/build_pve_shock_forester_acceptance.py`：93操作、141断言、4关键图。

- Commoners既有红蓝各1资源。明确五张自定原牌000/008/011/016/021，不冒充原标准16牌开局。
- Lv6托瓦克名望35，真实三个指挥槽，奇袭队/弩手/护林人，无技能；使用HeroBuild实际等级单位选择器。
- 首战既有敌群4原007狼骑兵甲4：奇袭远程1，弩手2+1合4，再加原利斧投掷1合5，实际远程击杀名望38；自己及原牌不获单位额外加值。
- 正式结束回合弃入5张原牌、宣布/结束轮/准备轮，同三部队重整，同玩家同单位身份保持。
- 次战既有敌群0原032哨兵：奇袭第二能力攻击4→3、护甲3→2；护林人格挡3实际防御成功且无伤；狂怒基础攻击2实际击杀、名望40。
- 两回合两战资源未消费仍红蓝各1；最后手牌0弃牌5，奇袭与护林人耗竭，弩手就绪。原032和狼骑兵是不同真实敌实例，不重复挑战已击败敌人。

主代理已实际运行并核对4图；本次读取实际report验证units.instances跨轮不变并生成验收索引。若失败保留原report、仅从失败/受影响操作补验。此生成器未触碰奇袭第三能力/护林人移动已通过范围。


## 16:43 实际场景验收完成

- `AutomationOutputs/PveBattle/20260929_164240_b5eac490/report.json`：一次成功93步、141断言；逐步success及所有assertions.passed均已实际读取核实。
- 4张关键图由主代理逐图查看正常：46弩手加值远程4、60同单位跨轮重整、78敌甲/攻击各减1、90狂怒基础攻击2实际击杀。路径写入 `pve_shock_forester_acceptance_index.json`，只保留本地。
- 原玩家真实身份在28/30/46/57/60/62/78/90/92共9个节点均为samePlayer=True。三个单位实例全程不变：`army_2_010:-474999942,army_2_012:1596819162,army_2_000:-2009524224`；ready从首战False,False,True到正式新轮True,True,True，再至次战False,True,False。名望35→38→40。
- 场景验证实际原牌远程贡献5、次战护林人格挡3成功、无英雄或单位新伤、正式两战两回合牌区/原红蓝库存保持，未重建玩家或单位。
- 上述三例旧后台XML再次定位Passed后作为复用证据；报告及XML SHA-256已写索引，不重跑既有通过测试。
- 仅更新本路线工作记录与索引，无生产/生成器修改。最终Unity Console双0由主代理本批统一补验，当前索引明确pending，不把其写成已完成。

本批末实际 Unity Console 连续两次零错误：`tmp/pve_puppet_shock_familiar_final_console.json`。
