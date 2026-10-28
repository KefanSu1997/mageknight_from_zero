# 四种原勇者：激活费用、交涉奖励及四种专属能力（待验证）

本轮01:30检查可用物理内存约0.70GiB、虚拟余量约0.74GiB，只做轻量源码与原素材核对，不启动.NET编译、Unity或新后台服务。02:20前不用Unity的约束有效。前一轮完成正式轮次边界源码/测试准备，属于实际进展，目标继续active。

## 原规则和实际入口

四张原图已在剩余部队审计中核对，本轮重看`army_en_008.png`明确公共文字是“任何突袭或攻击其他玩家时，激活这支部队支付2影响力”，不能泛化为所有战斗或防守另一玩家进攻。普通攻击/格挡5，以及影响5（交涉中使用声誉+1）为四种共有能力。

- `UnitJsonLoader`把原被动费用文字解析成`UnitCard.AssaultActivationInfluenceCost`，不伪装成第四个按钮；各卡保留3个可选择能力。四种原ID008/009/010/011加入加载，loader范围22→26，余4种（大师、魔宠、斥候、奇袭队）仍未开放。
- 四个专属能力分别为蓝费冰火格挡8、红费冰火攻击6、绿费治疗4、白费远程7。复用正式攻防贡献、治疗池及真实伤牌/按单位等级消费。原等级III/招募9、护甲4/4/3/6和冰/火/物/无抗性沿用已核对定义。
- `CardUnitActions.ActivationInfluenceCost`从本场真实`CombatSite`（要塞/法师塔/城市）或现有地道进攻玩家的`TunnelTargetPlayer`身份判断。怪物本身有城防不收费；移除/忽略城防不取消实际突袭的激活费；战斗结束以后不继续收费。一般PVP调度尚未完成，此处只覆盖既有进攻玩家身份，不宣称全PVP通过。
- 先预检影响力，再整笔支付能力魔力，之后扣影响力并执行效果。影响5不能先生成资源再补交本次激活费。战斗号召“视为已招募”的共用能力路径也走同一费用检查。
- 交涉声誉+1独立于通用无条件声誉变更，仅`InNegotiation`为真时发放，沿用正式交涉开始时冻结的声誉费用修正。负声誉开始交涉可能使影响力池暂时为负，0费用能力仍可发动，不能错误套用正费用余额检查。
- PVE UI显示影响力/声誉和当前额外费用，绿勇者战后只显示可用的治疗/辅助能力，不显示无效攻击按钮。静态跟踪同时发现上一批幻术师白晶虽然有按钮生成逻辑，却漏在战后面板入口判定之外，已补`GainCrystal`分支；这也是仍需运行UI验收的原因。

## 验证准备与缺口

新增`PveHeroesUnitTests`17个参数展开案例，未执行。覆盖四原定义、共同攻挡的真实消费、四种专属能力、原怪物自身城防与实际突袭的区别、影响/魔力缺一种时整笔保留、原承诺实际产生影响力并支付、影响能力不能自付激活费、正式交涉正/负声誉与实际招募、现有进攻玩家身份的费用。

静态校核突袭夹具时进一步按既有`EnemyTokenSupply.GuardColor`约束，使用原灰色铁甲战士025作为要塞守军、原紫色毒巫师002作为法师塔守军，并断言原标记颜色；不把普通绿敌或未标明供应色的敌人随意塞入城防地点来证明费用。普通遭遇的蓝冰火格挡仍以原巫妖003验证怪物自身城防不会触发突袭费。

`git diff --check`只验证差异格式，尚无编译/后台报告/Console双0/场景通过。资源允许后最新合并命令如下，复用22个轮末精确方法，避免全卡库和重复编译：

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$roundDeckFilters = Get-Content vibe_coding/codex/round_deck_lifecycle_filters_2026-09-23.txt
dotnet run --project tools/RulesCli -- . @roundDeckFilters MK.Tests.Cards.RoundFlowBoundaryTests MK.Tests.Cards.SpaceBendingBasicStateTests.OrdinaryRunOneTurnRetainsNormalRoundCountdown MK.Tests.Cards.ForestersMovementStateTests MK.Tests.Cards.BasicTerrainMovementStateTests MK.Tests.Battles.PveIllusionistSorcererTests MK.Tests.Battles.PveHeroesUnitTests
```

四勇者尚未生成Unity原部队资产并纳入可选合法构筑；交涉/地图/连续流程的完整场景入口仍待。因此不把loader26视为26/30完整Demo或场景通过，现有19/30场景基线不变。按全量初版后连续集中验收执行，不为四勇者各建一次性快照场景。后续Unity需核对费用显示、原图/原文字、绿勇者战后入口、幻术师战后白晶入口及必要布局。
