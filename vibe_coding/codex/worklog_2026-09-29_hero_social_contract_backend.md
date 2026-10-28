# 奥术伪装 / 特立独行正式交易后台（2026-09-29）

## 规则和实现

原英文技能图 `Assets/GameData/cards/skill_en_010.png` 的 Arcane Disguise 忽略声誉轨位置作用，不阻止声誉增减；中文旧概括有误。`HeroSocialSkillRules.Use` 将两技能的实际操作集中原子结算。`Options` 返回动态 `(Option, Label)`；`ClearTurn` 供正式回合结束清本回合字段。

- 奥术选0实际影响2；选1用 `player.FlippedSkills` 持久翻面，Context 本回合忽略声誉轨修正及-7交涉禁令。声誉值与奖惩保持真实改变。
- 交涉已开始才翻面时，从实际 InfluencePool 撤销恰好 InteractionAppliedModifier（负修正返还，正修正移除），冻结修正/已记修正同时归0，保留原暴徒/勇者整次交涉类型。
- 翻回100独立支付一点真实绿色魔力；不清本回合使用次数，也不撤销已授本回合忽略轨道许可。绿色不足不变翻面/次数。
- 特立选0实际影响1；选1检查此前无影响招募成功并授3，建立后续影响招募禁止。RecruitmentService 所有影响招募交易入口在付款前拒绝；折扣为0仍属于该交易。真正免费取得走 RecruitFree，既不记录此历史，也不受此承诺禁令。
- 主动可在行动前、当前交涉、合法正在进行的战斗使用影响；原规则未禁止战斗中的影响消费者。回合已完成非交涉行动/休息后拒绝；翻回独立任意时机仍要完成待决效果。

修改范围：新HeroSocialSkillRules；RecruitmentService；CardUnitActions仅-7条件；ActionContext三社会字段；独立PveHeroSocialContractTests。SkillActions早分发、旧Effect、正式EndTurn调用和UI由主代理接。

## 验证计划

新14项：正负及-7实际招募/声誉仍变，中途交涉正负修正，翻回缺费守恒与不重置，正式EndTurn跨回合持久翻面/许可清理，影响招募历史/未来/0费禁止及免费取得分离。实际CLI证据见下。原社会交易只限真实当前Context对应个人回合；未声称旧Adventure快照式教学流程适配。

新测试均改用主代理已接好的正式 SkillActions.Use 前门，连同绿费翻回和原个人回合清理一起验证。头环普通借用需要原自有技能上下文之外的专门许可，目前旧Effect会拒绝无真实持有者，不伪造所有权；保留为明确扩展入口缺口。

## 后台结果

`AutomationOutputs/RulesState/20260929_052835_8f50a877/results.xml`：52/52通过，包括新增社会技能14、原HeroesRecruitmentTests/ThugsRecruitmentTests受影响回归38。新增额外包含已实际招募原勇者/暴徒后翻面，撤销当次已双倍/反转的精确修正而不丢整次交涉类型；没有重复全卡库。代码冻结，Unity Console与场景由主代理完成。
