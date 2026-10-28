# 实际征服法师塔后自然招募及同部队消费者计划

## 状态与入口依赖

生成器 `tools/build_pve_tower_recruitment_acceptance.py`：80操作157断言4图，默认plan，--write及--from；本代理未操作Unity、未改生产/fixture/配置。新主session `BeginSiteInteraction` 与共用地点招募由主代理接入并完成对应后台后才能进入场景。本文件不是场景通过证据。

沿用共用 `JourneyInteraction/Btn_BeginInteraction`、`Btn_Recruit`，目标按当前真实地点和归属判定，而非脚本设Conquered。场景明确是已有演练世界真实塔/守军；不是原七格地图及后续原板块完整实现证明。

## 已核验旧证据和原供给

- 实际读取 `AutomationOutputs/RulesState/20260923_094234_80390e0d/results.xml`，`MK.Tests.Battles.PveWorldJourneyEntryTests.OriginalTunnelAttachesSameGuardAndSettlesActualSiteBeforeFormalTurnReward(MageTower,"monster_002")` Passed，复用其实际地道、守军身份、征服和正式奖励后台。相同run的Keep旧失败不覆盖、不改写，另有历史补测；本路线只塔。
- 实际读取 `AutomationOutputs/PveBattle/20260923_095116_b159f35f/report.json` 60..62：真实征服-1:-1、原002弃置、首供应法术005选择、正式末回合rewardPending清除。真实部队供给当时为 `army_2_001,army_2_005,army_2_006`，魔像已在当前可达供应，不需核心板块精英供应。
- `HornGold.asset`现有replayEffectRandom=1/seed=0，未改。用原目录卡构筑而非修改手牌/资源/守军。
- 原魔像army_2_001：II级、费用7、甲3物抗，城堡/魔法塔图标，普通攻挡2。原金币包items_010焚毁影响9/名望3。均从实际原JSON核对。
- 新自然地点交涉/本人征服校验后台须由主代理本批提供；旧村庄或地道证据不能代替这个新增门禁。

## 五原牌、三正式回合

合法Lv6托瓦克名望35、护甲3、3指挥槽，无起始单位。五原牌magic_004地道、magic_009火球、items_007绿戒、items_010金币包、items_015黑暗护符；既有红蓝库存各1。

1. 绿戒普通绿晶1/绿标1/名望+1，黑护符选黑魔力及白昼许可；地道强化实际支付绿黑，经山脉0,-1终点法师塔-1,-1，声誉-1，附接同一原002守军。
2. 火球付原红晶远程5击杀原甲5守军，名望40；正式走剩余战斗阶段、FinishSite征服、真实法术供应翻选奖励；正式EndTurn取得奖励原牌。手牌剩金币包+原奖励2张。
3. 第二正式回合仍在自己刚征服的塔；共用BeginInteraction应按真实MageTower地点允许。声誉-1先入账影响-1，金币包焚毁+9=8并名望43；实际当前供应魔像费用7，余1、真实新单位就绪、空槽2。EndTurn清余影响，奖励原牌仍在手。
4. 第三正式回合进入已有演练原032战斗（明确不是塔外地图导航完整证明）；跳过格挡，真实攻击4按Lv6甲3产生ceil(4/3)=2伤；新招魔像免费攻击2耗竭，保留的实际原奖励法术横置攻击1，合3实际击杀，名望45未跨升级阈值。正式EndTurn保留塔归属、同玩家/单位和真实伤2，最终手牌为2张伤牌。

四关键截图：实际征服；塔内费用7真实招募；同新招魔像攻击2；第三回合最终状态。验收需从报告比对新招单位instances与后战一致，原奖励在正式入手后才被消费；不能把正常运行状态代替图文核验。

无已有通过全库重跑，无临时种子更改，无目录伪造精英牌，无原地图来源替换。


## 执行前预期校正

主代理指出等级护甲预期错误后，已只读核实 `PveBattleController.HeroBuild.cs` 的 `2+(level+1)/4` 为整数除法，等级6实际甲3；`BattleResolver.cs` 正式伤牌计算为ceil(damage/armor)。因此原032攻击4产生2张伤，未达到该构筑昏迷阈值，奖励牌仍可横置；最后无可补原牌，手牌2伤。原脚本wounds1/hand1预期已改为2，没有改正式规则。构筑新增hero.armor=3断言；塔交涉新增实际siteRecruitLocation=MageTower、siteShieldInfluence=0和siteCanInteract=True三项只读断言。现为80操作157断言，后台门禁仍由主代理控制，不操作Unity。

## 实际结果

后台 `20260929_171747_fa03220e` 新21及直接受影响旧25共46/46通过。Unity编译后Console双0；domain reload期间一次MCP连接帧中断，等待并refresh(wait_for_ready)恢复，未重复请求编译或重启场景。场景 `20260929_172048_1890cc47` 一次80操作157断言通过，四关键图已目视。正式征服塔/回合末领奖→次回合金币焚毁9减原声誉1后8、付7招原魔像→下一战同魔像攻击2与保留奖励牌横置1击杀；Lv6护甲3真实两伤保留。最终Console双0见 `tmp/pve_site_recruitment_final_console.json`。详细报告哈希与单位身份索引 `pve_natural_site_recruitment_acceptance_index.json`。

当前已接地点接口含要塞/塔/城市，后台已过；本批真实场景只证明塔路线，不能扩写全部地点场景已通过。没有修改原卡/fixture/种子或推送远端。
