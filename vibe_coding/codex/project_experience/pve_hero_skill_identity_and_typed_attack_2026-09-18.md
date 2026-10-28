# 英雄已学技能与独立元素攻击（2026-09-18）

## 问题与最小修复

PVE 出战原先没有英雄技能入口。托瓦克的 `IceSwordEffect` 仍将近战池加2再写 `AttackElementChange[-1]`，不能把本技能的元素贡献与其他普通攻击独立结算。

- 复用 `CardJsonLoader` 的正式技能映射，提取 `ParseHeroSkills(json,poolId)` 供打包后的 TextAsset 和本地测试共用；不在 UI 重造技能数值。
- 独立 `BattleHeroDefinition` 绑定原技能池和原图区域；出战配置仅授予明确的已学索引，检查重复和等级允许数量。首次两套快照均为名望3/等级2、已学1技能；不是开局免费拥有整池技能。
- `PveBattleSession.UseSkill` 限制已接入选项及合法战斗阶段，然后调用正式 `SkillActions.Use` 核对实际所有权及使用次数。战斗结束不能重置每回合次数。
- 寒冰剑术改为 `CombatPower.AddAttack(2, Ice/Physical)`，新增选项验证；不污染其他卡牌贡献。炽热剑术复用既有实现。
- UI 选择技能、选择普通/元素分支、确认；显示原文与使用状态。原图只使用 RawImage.uvRect 显示对应行，未重绘或新增伪造卡面。

## 原素材映射教训

`hero_skill_pool_007` **不对应** `skill_007.png`。实际寒冰剑术位于 `skill_021.png`，炽热剑术位于 `skill_000.png`。按原图行内容核对，不能用数字后缀猜映射。当前英雄立绘仍沿用通用演练占位图，不将其记为原英雄专属立绘完成。

## 验收

- 后台 `MK.Tests.Battles.PveBattleSkillTests` 5/5，`AutomationOutputs/RulesState/20260918_162848_a19ff3f8`。
- 场景 `AutomationOutputs/PveBattle/20260918_163116_73265a64/report.json`：4场、110次真实按钮点击、278检查通过，9张关键图已查看。两技能各普通/元素分支，非法同回合重复不增加资源。
- 普通技能2+真实横置1击杀护甲3哨卫，名望3→5；火/冰技能2+狂怒普通2对物抗兽人有效3，名望3→7；全场无伤。后台还检查非法阶段、非法选项、外来等价实例和同回合下一战拒绝。
- Logic DLL16:30:39，Scripts/Tests16:30:42；场景前Console两次0，最终 `console_final.json` 两次0。
- 本批只证明2技能，不代表两英雄全技能通过，更不代表全部英雄。其余未接入项继续逐批核查。
