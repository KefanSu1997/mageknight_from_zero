# 奥术声誉轨与影响招募承诺经验（2026-09-29）

原英文 Arcane Disguise 忽略 reputation track position effects，不等于禁止真实 Reputation 数值变化。必须保留原英文纠错证据，不能对所有声誉写入套冻结。正式 BeginInteraction、GetCost、TryRecruit 和封装的 -7 拒绝必须同源判断本回合轨道许可。

中途翻面需撤销已入池的 InteractionAppliedModifier，不能用当前 Reputation 再算一次，因为整次交涉此前可能已冻结、由暴徒反转或勇者翻倍。InfluencePool 减已记值，两个修正字段归0，原交涉类型保留；正修正可能已消费，撤销后池为负是精确账面结果，不能凭空夹为0赠资源。

翻面持久存在 player.FlippedSkills；绿色翻回只恢复技能正面，不清本回合次数，也不撤销“本回合”的既得许可。个人正式 EndTurn 清许可，下一回合不能凭仍翻面延续免声誉。

特立独行限制的是影响招募交易类型，不是最终费用大于0：折扣至0仍禁止。成功历史仅在 RecruitmentService 完成实际招募时记录；真正 RecruitFree 不记该历史，免费取得不应被其阻止。既检查过去又约束未来，失败前无供应、费用、次数变化。

后台证据：`AutomationOutputs/RulesState/20260929_052835_8f50a877/results.xml` 14新增+38受影响回归通过。场景验收另记，头环普通借用的非自有上下文当前为明确缺口，不伪造持有技能。
