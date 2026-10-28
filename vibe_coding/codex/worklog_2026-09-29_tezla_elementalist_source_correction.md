# Tezla元素派系图标纠错：仅确定数据，奖励仍待模式

主代理已查看官方缓存 `tmp/pdfs/tezla_official_rules.pdf` 第2/3页和原怪015/054/057图：名望旁绿蓝图案为Elementalist派系，不是Regenerate能力。官方派系奖励有token或Just Fame变体，主代理正在等待用户选择；本次不自行选择或预先增加名望。

已仅改 `resources/text_json/monster.json` 与 `Assets/GameData/BattleDemo/original_monsters.json` 的015/054/057三个记录：删除abilities中的regenerate，保留空能力列表，新增独立 `faction: Elementalist` 原义元数据。未改其他怪、印刷名望3/4/1、原攻击、护甲4/7/1和火/冰抗性；未改Enum及MonsterJsonLoader生产接口。新字段暂不进入正式Monster状态，派系奖励因此仍未完成，不把图标纠正记为完整规则验收。

独立定向测试 `Assets/Tests/RulesState/TezlaElementalistSourceDataTests.cs`，唯一入口 `MK.Tests.Battles.TezlaElementalistSourceDataTests.ThreeOriginalElementalistSymbolsAreMetadataNotRegenerationWithPrintedStatsPreserved`：两JSON语义完全一致；三记录元数据准确；两侧正式ParseMonsters不产生Regenerate且保留原攻击/抗性/护甲/印刷名望。未执行测试或Unity，由主代理统一运行。

实际流向：CLI `CardJsonLoader.LoadMonsters`读resources；PVE `BattleEncounter.LoadEnemies`读打包TextAsset，二者同`MonsterJsonLoader.ParseMonsters`。015当前已被Witch/FearPatrol/WitchSentry遭遇使用；054/057虽无当前PVE固定遭遇及worldEnemyCatalog入口，仍被大师、叉形、头环后台使用。三者均不在当前七项世界敌供应表。PVE能力显示由`PveBattleController.Abilities`从正式Abilities生成，源数据移除错误枚举后不再显示再生。

后续奖励不能只改一种结算：原远程`RangePhaseResolver`、近战`BattleResolver`和直接消灭`CardCombatActions`各使用敌名望。派系应作为独立身份，原法术/洞悉不可将派系当可移除能力；印刷名望与所选奖励模式应分开表达。以上是后续实现建议，本次没有修改这些路径，也未声称奖励完成。

## 定向结果与新确认边界

已实读 `AutomationOutputs/RulesState/20260929_201953_129251ff/results.xml`：唯一数据方法1/1通过、37断言。这仅证明本次删除误识别、保留此前已记录的印刷数值，以及两份数据一致，不证明三原怪全部原规则正确。

主代理在官方Tezla规则第2页Defend、第4页Elusive与057原图确认：冰蝠另有双护甲1/2及右上盾1。当前JSON只保存armor.value=1，未保存另一个护甲或Defend数值，这是新增明确原数据/实现缺口；不能宣称剩余仅派系奖励。原测试“printed stats preserved”指没有顺带改变旧数值，不代表对未建模的双护甲规则背书。

只读定位：当前 `Assets/Logic/Core/Enum.cs` 无Elusive/Defend敌能力；`Assets/Logic/Data/Data.cs`的Monster只有单Armor，`MonsterJsonLoader`也只读armor.value。`Assets/Logic/Runtime/CombatMath.cs::EffectiveArmor`只有单护甲+城市加值-减甲，以及原疾病的BlockedArmorOne分支，并没有Tezla双护甲/Defend实现。不要把玩家部队Ability.Guard或地图Defenders名称当作Defend规则。

可复用的是实际敌引用、`ActionContext.SuccessfullyBlockedAttacks`与正式`BattleResolver`逐槽结算；`WasBlockedThisBattle`只有被标记成功的敌引用，是否足以用于多攻击Elusive须按原条款判断，不能未经核对直接复用。`CardCombatActions.DefenseProfile`/`CombatMath.EffectiveArmor`与现有敌数值UI是后续集成点，仍需原字段、原适用时机和正常消费者，不是配置一枚已存在能力即可完成。本次未改这些源码/其余怪物，奖励模式和057两能力均待后续决定/实现/验证。

## 主代理最终显示证据

202312_1e4dc463成功22总操作（21纯重放）、6断言、1关键图，原015不再显示再生，火冰攻击及抗性保留，主代理已目视。编译tmp/pve_elementalist_label_compile_console.json与最终tmp/pve_elementalist_label_final_console.json各双0。奖励系统、057双甲和Defend仍未完成，独立索引pve_elementalist_label_acceptance_index.json。
