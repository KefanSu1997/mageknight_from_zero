# 勇者真实突袭附费：单代表连续路线准备

当前仅完成独立生成器与独立 RulesState 测试源码；**未运行后台或 Unity，未声称通过**。生产、共享 fixture、配置均未改动。

## 入口审计

未发现必须修改生产的断路：`TunnelTravelEffect` 正式把真实 `TunnelTargetSite` 写入 `CombatSite`；`CardUnitActions.ActivationInfluenceCost` 读取 Keep/MageTower/City 或真实进攻玩家身份，地道忽略地点城防不会豁免2影响。`PveBattleSession` 对原承诺 basic_card_008 提供战内 base/strong 选项。故在实际地道开战后使用原承诺，避免提前交涉/行动导致地道非法。

四原勇者共用同一附费字段与消费实现，只用白勇者代表这一公共规则；其独有白费远程7本已有证据，不再四色重复。普通城防怪物不构成此验证。

## 合法路线与费用

`tools/build_pve_heroes_assault_fee_acceptance.py` 只复用既有法师塔路线起点 UI：HornGold 库存红/蓝各1、无初始单位。合法原托瓦克等级4、名望15、两指挥槽选一支原白勇者 army_011。五张原牌是承诺、行进、地道、绿宝石戒指、黑暗护符；不是标准16牌开局，也不冒充自然招募。

1. 原绿戒正常获得绿晶1、绿标1、名望+1至16。
2. 原黑护符焚毁，独立选择 Black/White/White，得到黑标1、白标2及白昼黑许可；护符实际移除。颜色输入初始 Red/Blue/White，按5/2/0次循环，未修改UI默认或共享fixture。
3. 原地道强化支付实际绿标1+黑标1，经过山脉0,-1到原实际塔-1,-1，与原紫色守军002（甲5）开战；实际位置/地点/守军保留，声誉-1，忽略地点城防，白标仍2。这里不直接赋 `CombatSite`。
4. 战内原承诺基础免费产生影响2。白勇者原能力1整笔扣影响2与白标1，贡献远程7并耗竭；余白标1。截图记录实际费用和贡献。
5. 正式远程7击杀同守军，名望16→20、无伤。完成剩余阶段，实际征服塔、守军弃置，截图；选择实际供应第1张法术奖励。
6. 正式 EndTurn 领取奖励、清临时白标/影响/昼黑许可；保留行进+奖励两手牌及同勇者耗竭。BeginTurn 开始下一回合，同玩家、同勇者、同征服位置继续，截图。没有重建战斗快照。

## 独立后台门禁（必须先跑）

新增 `Assets/Tests/RulesState/PveHeroesRealAssaultJourneyTests.cs`：

`MK.Tests.Battles.PveHeroesRealAssaultJourneyTests.ActualTowerTunnelKeepsAssaultFeeThenSameHeroesAndRewardSurviveFormalTurn`

该单例按同五牌与颜色付款，通过实际 `PveJourneySession.PlayOutsideWithInputs` 开始真实塔战，检查 `CombatSite` 和守军引用一致；先在无影响时尝试能力，确认拒绝且白标2/部队就绪不变，再真实承诺产生2后支付；实际击杀、征服、奖励与正式下一回合同实体。地图为独立合法有限测试地图，未改共享场景或fixture。此例用来补原旧后台手设CombatSite与真实地道入口间的组合依赖，不能仅凭既有单能力通过跳过。

主代理先用既有 RulesCli 入口仅运行该确切方法；若失败，保留失败并只修/补本方法或受影响规则，禁止直接反复进场景。新测试在 Assets 下，编辑器可能自动导入；最终由主代理统一取得 Console 两次零错误。

## 场景入口

```powershell
& 'D:/study_and_work/unity_mcp_source_code/unity-mcp/Server/.venv/Scripts/python.exe' -X utf8 tools/build_pve_heroes_assault_fee_acceptance.py
& 'D:/study_and_work/unity_mcp_source_code/unity-mcp/Server/.venv/Scripts/python.exe' -X utf8 tools/build_pve_heroes_assault_fee_acceptance.py --write
```

默认 plan only；`--write` 后由主代理串行调用既有 Unity 自动化。失败补验支持 `--from N --write`，完整重放合法前缀但清除已过断言/截图。不得把本计划更新为勇者费用场景已通过，也不顺带关闭精英招募翻倍或PVP完整规则。


## 后台门禁与UI路径冻结

主代理串行执行 `20260929_183114_13350633/results.xml` 后，本路线唯一方法 `MK.Tests.Battles.PveHeroesRealAssaultJourneyTests.ActualTowerTunnelKeepsAssaultFeeThenSameHeroesAndRewardSurviveFormalTurn` 实际 Passed。同批 Arythea 方法 Failed，**不记2/2或整批全绿**。`tmp/pve_social_assault_compile_console.json` 两次实际返回 data=[]，编译 Console 双0；这不是场景最终 Console。

只读复核当前UI源码：ChooseCards重置kind0，ClearDeeds只清所选；承诺/行进两按钮在基础首页，后续法术/神器分类切换沿用原塔前缀。部队目录页按真实scene guid映射定位，白勇者能力1按钮进入正式UseUnit。Artifact输入原Red/Blue/White且六色循环，Black/White/White的5/2/0正确。当前Journey状态提供 tokens.white、world.tunnel、world.ignoreSite、samePlayer等观察key；战内承诺base选项存在，远程阶段可发动。未发现需修生成器的路径或key缺口。

默认plan只读执行输出 **62操作、95断言、3截图**，未写配置：23步构筑起点；51步整笔付款、58步正式征服、61步次回合为三图；60步正式回合末。生成器现冻结，主代理可在当前后台通过基础上串行 `--write` 并运行场景。当前仍未记场景通过。


## 实际场景通过，共用收费退出待测

`20260929_184358_8671e144/report.json`一次success：62总操作/95断言，20无断言起点操作不计新增，42带断言步骤。51/58/61三图由主代理逐图目视正常。报告23/51/58/60/61全部为 `army_011:1617467962`，samePlayer全部True。51步实际白标2→1、影响2→0、远程0→7、就绪True→False；58步真实塔征服；60步临时白标1→0并正式奖励使手1→2；61步同单位耗竭、塔位置-1,-1、名望20保留。

生成 `pve_heroes_assault_fee_acceptance_index.json`；unit ledger四ID的共同收费记录由同一白代表更新为有限场景通过，**不是四色各跑一次**。精英招募声誉翻倍缺口原样保留，hero ledger未动。

最终Console已核通过：`tmp/pve_social_assault_final_console.json` 实际两次 success、result.success=true、data=[]，即错误数0/0；本次最终检查独立于此前编译门禁。索引状态已改passed。
