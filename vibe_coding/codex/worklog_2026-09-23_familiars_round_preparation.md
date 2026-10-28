# 魔宠原规则与真实新轮准备

## 范围与依据

- 复用 `army_2_004` 原定义及 `Assets/GameData/cards/army_2_en_004.png`，等级II、影响力6、护甲5，修道院/魔法塔/魔法空地。动作无再次激活魔力费；招募另付一基本色魔力，颜色留在该实体部队上。
- Lost Legion 原扩展规则PDF第5页（页码5、PDF索引4）明确：魔法空地招募占本回合行动，但不算交涉，不吃声誉调整/“交涉时”效果；魔宠不能免费取得或被战斗号召借用；新轮在战术选择前支付库存魔晶或移除。
- 来源：https://www.mageknight.net/wp-content/uploads/Mage-Knight-Board-Game-Expansion-The-Lost-Legion-Rules-2013.pdf 。只读取规则所需章节，没有下载大PDF或上传截图/日志。

## 修改与原因

1. `UnitJsonLoader`开放第29种部队，按原文解析四动作及维持被动；原JSON补仅交涉取得限制，加入魔法空地地点位。原动作3/4/3/2分别受红+2、蓝+3、白+2、绿+1颜色标记影响；移动/影响二选一。
2. `UnitState`保存独立颜色和待维持状态，未定色/待维持不能发动。`FamiliarActions.AbilityValue`供正式能力消费和后续显示共用。魔宠颜色不加入玩家临时魔力，也不会因发动而被消耗。
3. `RecruitmentService`和`CardUnitActions.TryRecruit`增加显式选色，先查地点、影响力、指挥槽，再原子付实际魔力和取得供应。失败不扣影响力/魔力、不移除供应、不提交行动。空地招募走非交涉分支；同一空地招募行动的状态在正式回合末清理。
4. `GameEngine.PrepareNewRound`执行新轮补牌/部队重整等已有逻辑，魔宠进入待维持；`ResolveFamiliarUpkeep`仅允许实际玩家对自有实体在战术前选择库存基本色魔晶或移除。库存支付不借用临时标记、不作为回合内施法支付/退款。受伤仍保留；放弃维持销毁并移除同一实体。`StartNewRound`只在所有待维持解决后提交战术，不重复补牌/重整。已有无魔宠调用保留兼容自动准备；准备中禁止跳过直接结束轮次。
5. `PveBattleSession`允许魔宠影响力分支沿用实际战斗影响力消费；原攻击、格挡、治疗共用正式消费者。完整非战斗UI/原图/新轮操作仍随连续控制器接入，没有给魔宠另建一次性快照或冒充已完成场景。

## 必要验证与修复

- 初次CLI构建暴露新测试误用不存在的`ActionContext.AttackPool`；修为正式`PveBattleSession.AttackTotal`，未改预期或正式规则。
- 一次合并定向后台：`AutomationOutputs/RulesState/20260923_025452_e400c660`，**55/55通过**，无跳过。选择范围：`MagicFamiliarsStateTests`、`RoundFlowBoundaryTests`、`RoundDeckLifecycleTests`、`HeroesRecruitmentTests`、`PveBattleUnitTests`。共同招募与轮次入口发生变化，因此回归其直接消费者；没有重跑整库/此前143或44批次。
- 魔宠证据覆盖四选色、真实招募支付、声誉±7空地非交涉、英雄交涉奖励不触发、费用/槽/地点/行动失败守恒、免费取得/号召拒绝、不同颜色攻击/格挡/治疗的真实消费、白色移动/招募消费、同玩家同牌同受伤部队跨昼夜、先维持再战术、临时魔力拒绝、两魔宠独立移除/维持。
- 刷新现有Unity（无新进程）；刷新触发域重载，首次连接返回Incomplete MCP frame，没有立即重复刷新，待重载后读取Console。`tmp/pve_20260923_familiar_console.json`：两次 `types=[error], count=1000, includeStacktrace=true`，均0。
- 尚未制作/执行魔宠独立场景批次；按用户新节奏等完整合法入口和连续路线集中验收。旧完整部队场景基线仍**19/30**，不是29/30；Unity场景原资产目录仍28，未运行Build自动加入无选色入口的魔宠。

## 后续

- 完整初版还缺斥候规则及其地图/供应窥视入口；已查看 `army_2_en_009.png`：攻城1、私看3格内盖放标记或任意未用敌堆顶并对本回合实际击败同标记+1名望、移动2和2格探索。不能把私看替换成全局翻开，不能奖励同名不同实体。
- 接着补全连续控制器中的正式招募、魔宠选色/维持、地图、战术和同实体后续战斗，再集中跑实际场景。
- 另已发现现有 `ExplorationService.ProcessTileEvents`仍是随机简化生成，并非按原板块内容生成；连续原版地图接入时必须处理并独立记录，不能把现有后台探索距离/费用通过等同于完整原版板块内容通过。
- 总目标保持active；初版/后台/场景三层证据分开，未增加全能力部队场景通过数。
