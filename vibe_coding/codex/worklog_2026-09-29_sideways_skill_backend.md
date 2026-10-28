# 通用力量 / 漫不经心后台接入（2026-09-29）

来源：`Assets/GameData/BattleDemo/original_skills.json` 与正式 hero_skill_pool_005:07 / hero_skill_pool_007:06。通用力量每回合一次，为一张横置牌支付一点魔力：3；原行动（含高级）/法术同色则4。漫不经心每回合一次：基础2，高级/法术/神器3。

实现：新增 `SkillActions.PlaySideways(player,context,skill,card,type,color)` 单笔入口，检查实际自有未用技能、真实手牌引用、原定义类别、用途与实际选色费用后，复用 `ActionSystem.PlaySideways` 的内部单次值参数。无新的悬空许可或永久全池加值；原效果的旧无目标 Execute 拒绝，不能用 option 猜卡类别绕费用。原定义/引用/费用失败发生在付款前，成功记录正式已出牌区和技能每回合次数。后台入口交由 session 继续限制正式战斗阶段及战外时机。

验证：新增 `PveSidewaysSkillTests` 21/21，通过目录 `AutomationOutputs/RulesState/20260929_034025_46126f3d`：原卡类别、同色/异色、实际token优先于晶体、四种横置池、只一牌/每回合一次、失效目标/非法类型/费用与选色守恒、旧入口拒绝、正式回合末弃牌。首次CLI构建仅新增测试误传StartTurn的ActionContext，修正为DayPart后通过。共用横置入口直接受影响 Pain 方法1/1通过：`AutomationOutputs/RulesState/20260929_034100_75b9af40`。无整库重跑。

边界：伤牌默认不能横置，本入口明确拒绝；与其它技能授权伤牌横置的组合未列入本批基本范围。金黑实际标记支付走同一精确支付入口，但本批新增测试未单独覆盖。Unity编译、UI与连续场景由主代理统一执行，后台通过不记为场景通过。
