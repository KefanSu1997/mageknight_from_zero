# 幻术师与巫师原能力接入（源码完成，待验证）

## 执行边界

前一目标轮完成护林人共享移动入口和测试准备，属于实际进展。本轮继续低资源源码工作：01:12虚拟内存余量约1.17GiB，未启动编译或Unity。02:20前不用Unity的约束仍有效，不改VPN、系统或运行中的游戏。目标仍active。

## 原图与实现

本轮重新查看`Assets/GameData/cards/army_2_en_003.png`及`army_en_014.png`确认费用/阶段图标，使用已修正的原JSON：

- 幻术师：II/费用7/护甲2物抗，法师塔或修道院；影响4免费、白费使一名无城防敌人本场不攻击、免费获得白晶体。停攻带格挡图标，只在格挡阶段，不能把奥术免疫或城防目标先扣费后报错；白色效果不继承蓝色冰抗限制。停攻与成功格挡记录分开。
- 巫师：III/费用9/护甲4火冰抗，法师塔或修道院；免费远程3、白费目标失去全部城防+远程3、绿费目标失去全部抗性+远程3。目标校验、支付后分别应用实际敌人修正和独立攻击贡献；奥术免疫仅阻止敌人修正，保留独立远程3，与现有原暴露逻辑一致。
- 复用`ExposureEffect.Validate`的本场存活目标校验、`CombatMath.IsFortified`实际城防、`TryPayExactColors`整笔费用和`ManaPool.AddCrystal`库存上限/溢出。所有修改使用既有敌索引重映射及战末清理；未新建近似战斗引擎。
- `UnitAbility`增加独立晶体/无城防停攻类型，远程原能力附可选去城防/去抗性字段；其他旧能力默认不变。`UnitJsonLoader`接入两种原卡，目前加载范围22，剩8种仍拒绝，不因目录存在开放。
- `PveBattleSession`接目标/阶段与晶体入口；`PveBattleController`支持选择目标后发动、显示费用、战后白晶。承伤阶段幻术师拥有影响/白晶两个选择，使用按需能力面板并可返回承伤安排，避免只暴露第一个能力。保留原单位按钮ID。调整同时带强韧旌旗及辅助能力时的承伤面板纵向位置以避开重叠；实际截图待验。

## 后台与场景状态

新增`PveIllusionistSorcererTests`15个参数展开案例，均未执行：

- 影响4真实招募原农民；白晶及满库存溢出实际支付巫师白费并击败原巨鹰。
- 幻术师真实取消原兽人攻击、不算格挡、原攻击击杀后的战末清理；城防、地点城防、奥术免疫及错误阶段在支付前拒绝。
- 巫师三种原能力支付各自费用，对原兽人/巨鹰/寒冰巫妖实际远程消费；去城防和去抗性互不混淆，战末清理；免疫、缺费用和未选目标的守恒检查。

静态检查修正了一处新测试对已消费魔力标记的直接字典索引：正式支付会移除0值标记键，断言改用GetValueOrDefault，不修改实际费用预期。`git diff --check`通过不代表编译通过。

资源允许时合并当前待验范围只编译一次：

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$roundDeckFilters = Get-Content vibe_coding/codex/round_deck_lifecycle_filters_2026-09-23.txt
dotnet run --project tools/RulesCli -- . @roundDeckFilters MK.Tests.Cards.ForestersMovementStateTests MK.Tests.Cards.BasicTerrainMovementStateTests MK.Tests.Battles.PveIllusionistSorcererTests
```

尚无后台报告、Console双0或新场景证据。新部队定义尚未通过Unity原资产生成入口同步到演练构筑；完整连续地图/交涉与构筑入口也未完成。因此本批只是正式规则和UI接线，不增加“完整Demo已接入”或19/30场景通过数。不单独新增两个独立验收快照，后续随合法构筑连续流程集中验收。界面仍需检查目标选择、承伤辅助面板、长文本及附旗时按钮布局。
