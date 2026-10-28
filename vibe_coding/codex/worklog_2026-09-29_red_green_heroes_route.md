# 红绿勇者连续路线计划

状态：生成器就绪；未运行Unity，不记场景通过。

`tools/build_pve_red_green_heroes_acceptance.py` 默认仅plan，支持 `--write` / `--from N`。105操作168断言4张截图。不修改任何生产代码、配置、fixture或种子。

合法等级6托瓦克、红勇者/绿勇者/幻术师三槽。原五牌同上一条路线、Commoners红蓝各1晶，不冒充原16牌开局。

第一回合鲜血仪式基础产生原手伤1、额外红晶和绿标；幻术师白晶能力先耗竭。实际冰巫妖寒冰攻击2由同幻术师承伤，护甲2且无冰抗，真实部队伤1；旧手伤仍1。红勇者红费冰火攻击6消费并击败有物抗+冰抗原004。战后绿勇者支付同绿标得治疗4，按幻术师真实等级2治部队、再1点治原手伤，保留耗竭不重整。剩余1点正式回合末清理。

正式换轮洗回同五牌、同部队重整。第二战绿勇者原免费格挡5抵挡032攻击4，红勇者原免费攻击5击杀甲3；同单位跨轮复用，未重建玩家。

后台复用已实际读取的 `AutomationOutputs/RulesState/20260923_021816_212ab687/results.xml`：
- RedColdFireAttackConsumesRedAndDefeatsOriginalIceResistantEnemy Passed
- GreenHealingPaysForActualLevelThreeUnitAndOneHandWoundWithoutReadying Passed（后台等级3部队；本场景实际等级2部队，按正式等级消费）
- OrdinaryAttackOrBlockUsesFiveWithoutAssaultSurcharge(false/true) 为同组方法，主代理可按需核实XML。

主代理需核对四关键截图、真实单位instances以及实际伤牌状态；尚未主张通过。本批不增加边缘组合，不重复已过蓝勇者。


## 实际完成
20260929_162047_17f0577f 一次105操作168断言通过，四关键图已目视。七处三部队instances完全一致；真实旧手伤、幻术师承伤、红冰火攻击6杀有冰/物抗004、绿费治疗4按等级2花点后治旧手伤1、剩1正式清理，正式新轮才重整。第二战同单位免费格挡5与攻击5、原伤0、红白蓝各1和正式两回合两战全通过。索引pve_red_green_heroes_acceptance_index.json；最终Console双0 tmp/pve_sorcerer_heroes_final_console.json。无fixture/种子修改，原后台4项XML复用不重跑；本场景不宣称勇者交涉亦通过。
