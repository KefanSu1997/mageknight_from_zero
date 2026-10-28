# 护林人原能力接入共享移动服务（待验证）

用户游戏期间继续轻量源码工作；本轮01:08检查可用物理内存约0.71GiB、虚拟余量约1.37GiB，未启动.NET编译或Unity，未修改网络/系统设置。上一轮完成轮末预期和22个精确方法筛选，本轮为新增规则接入，目标继续active。

## 原规则与实现

原英文卡面`Assets/GameData/cards/army_2_en_000.png`在前一批已核对，既有中文名为“护林人”：I级、招募影响力5、护甲4、村庄招募；免费移动2，本回合森林/丘陵/沼泽费用-1且最低0；另一能力为免费格挡3。

- `UnitJsonLoader`严格解析既有原文本，将`army_2_000`加入正式加载范围；不放开其他尚未完成的10种部队。
- `UnitAbility`补可选的地形减费字段，保留Movement类型及所有旧构造调用。`CardUnitActions.Use`生成真实移动池和回合减费，仍检查所有权、就绪/创伤并耗竭实际单位。
- `ActionContext.TerrainMoveReductionToZero`独立保存最低0的减费。`MovementService.GetCost`沿用真实地图、昼夜、不可通行校验和旧最低2点减费，再应用允许到0的部分；`TryMoveUsingPool`仍是实际消费入口。没有改旧卡最低2点的语义，也没有用修改目标费用的演示数值代替移动。
- 正式回合结束的`ClearMovementAndReadyEffects`清理此减费，普通下一回合不重整部队；原格挡3继续使用既有战斗入口。

## 验证准备和缺口

新增`ForestersMovementStateTests`共10个参数展开用例：昼夜森林/丘陵/沼泽/未减费平原的实际移动与原行军补点、原冰霜之桥与护林人两种先后顺序降到0并在真实回合结束失效、湖/山不可通行、原敌人的真实格挡消费。只涉及主要原效果及直接消费，不穷举组合。

本修改触及共享费用服务，定向回归既有`BasicTerrainMovementStateTests`，核对无护林人时冰霜之桥/清风/迷途的既有印刷费用与限制。资源允许后和轮末批次一起只编译一次：

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$roundDeckFilters = Get-Content vibe_coding/codex/round_deck_lifecycle_filters_2026-09-23.txt
dotnet run --project tools/RulesCli -- . @roundDeckFilters MK.Tests.Cards.ForestersMovementStateTests MK.Tests.Cards.BasicTerrainMovementStateTests
```

目前仅`git diff --check`通过，不是C#编译或后台测试通过。正式loader范围从19变为20，但Demo还没有连续地图移动入口，尚未同步Unity打包JSON和可选构筑，不记作第20种Demo完整接入；既有19/30部队场景证据不变。下一步先运行后台范围，初版地图/连续控制器完成后集中操作验证原图、费用、实际位置、剩余移动、单位耗竭和下一回合失效；Console双0及场景均待完成。不新造独立测试场景或快照来冒充完整初版。
