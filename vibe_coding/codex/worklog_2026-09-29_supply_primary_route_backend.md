# 原供应主要模式：后台预演与连续场景脚本（2026-09-29）

## 本批边界与实现

未改生产规则、既有 loadout、Unity 场景或原卡素材。新增 `Assets/Tests/RulesState/PveSupplyPrimaryModesTests.cs` 两条真实后台路线，以及 `tools/build_pve_supply_primary_modes_acceptance.py`。场景由主代理单独执行；本记录不声称场景已通过。

原来源为 resources/text_json 中 basic_card / advanced_card / magic 的既有定义，实际效果由 PveJourneySession / PveDeedInputs / CardPlayActions 执行。两条均艾瑞希亚等级1、无技能无部队、红蓝各1晶的自定快照，明确不是原英雄标准16牌开局。持有三张唯一高级牌026/027/033会先从实际供应排除，再随机洗混；这是真实供应所有权约束，不能照搬不同构筑的种子供应。

## 后台证据

- `AutomationOutputs/RulesState/20260929_153255_242e91eb/results.xml`：2/2 Passed，分别20、18条 NUnit 断言，共38。
- 只测上述两条新的路线依赖；单卡旧规则证据复用 `pve_supply_deeds_acceptance_index.json` 与 `pve_deed_inputs_acceptance_index.json`，未重新跑全库。
- 保留失败预演：`20260929_152842_e095bcf4`、`20260929_153020_4da62f5c`（少持有一张高级牌，供应变化；第二次只增诊断，无规则变化）；`20260929_153152_48151ac5`（把蓝色耐力014误当格挡牌、零绿晶字典不存在）。修正构筑/路线/观察方式，没有改规则、放宽预期或改原卡。
- 原耐力014实际蓝色强化移动4，已按正式效果与公开 PVE 选择更正。最终两种来源的魔晶分别支付绿色行进和蓝色耐力，8移动实际支付平原2余6，再正式结束回合清零。

## 场景计划与执行方式

脚本创建文件本身不写当前 AutomationConfigs；已仅向 `tmp/supply_primary_learning_preview.json`、`tmp/supply_primary_practice_preview.json` 做结构预览。主代理在当前其他场景结束后执行：

```
python tools/build_pve_supply_primary_modes_acceptance.py learning --prepare
# 主代理执行既有 Run Acceptance
python tools/build_pve_supply_primary_modes_acceptance.py practice
# 主代理执行既有 Run Acceptance
python tools/build_pve_supply_primary_modes_acceptance.py --restore
```

`--prepare` 精确备份原验收配置；后续写入留下 SHA256，`--restore` 只有当前配置仍与本生成器最后输出一致时才恢复，避免覆盖他人修改。所有模式均不改 HornGold 资产或种子。失败使用相同 route `--from N`：前缀真实重放，但关闭旧前缀断言与截图。`--output path` 可只写临时预览，不占用主代理配置。

### learning：71步、115断言、3关键图（预计，尚待场景）

- 七张原牌000/008/014/022/高级026/027/033。
- 强化熔炉支付蓝1→0，选择实际法术004绿色、023白色，两法术均留供应，绿白各1。
- 强化学习支付白1→0得影响4；真实源中第一枚白骰供承诺强化4；威吓基础2，影响10。实际选择供应中的风暴022，支付9余1，加入手牌。
- 同一风暴基础选择真实蓝骰（原白骰已被正常取用），得蓝晶并同骰重掷归源，不伪造骰面。
- 绿晶实际支付行进强化4、蓝晶支付耐力强化4，从原位置走实际平原，8扣2余6。正式回合末弃剩余027，8张真实原牌均入弃，移动清零，位置和玩家保留。
- 关键图：两个实际法术选择；9影响力购牌面板；真实移动费用与结果。

### practice：66步、110断言、2关键图（预计，尚待场景）

- 五张原牌014/021/高级026/027/033。实际三高级牌持有使供应仍005/022/003，不假造供应。
- 修行基础选择蓝色耐力014永久移除，选择同色005寒冰之盾进入弃牌，不付强化绿费。
- 正式结束回合丢弃三张余手牌，弃牌总5；真实空库声明轮末→结束轮→新轮准备，正式洗混并抽满5。五张全抽保证取得盾可用，顺序仍随机，界面按实际可见原卡查找。
- 同一玩家正式夜回合原盾蓝费得寒冰格挡3，加熔炉原横置1挡实际哨卫攻击4；正式防御无伤，原盾附效使同敌护甲3→1。
- 狂怒原红费攻击4击杀同敌，名望0→2；正式结束第二回合原盾回弃，耐力仍在永久区，玩家与牌身份保留。
- 关键图：实际原材料/同色供应选择；跨轮抽回原盾的真实格挡减甲结果。

## 尚待验收

主代理已告知新测试 Unity 实际编译 Console 双0：`tmp/pve_supply_primary_compile.json`。两条场景及关键截图仍待主代理跑完后记录有效证据；不能把本后台通过替代场景通过。
