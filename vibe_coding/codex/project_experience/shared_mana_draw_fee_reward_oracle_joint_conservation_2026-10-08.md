# 魔力汲取与魔力强化付款和奖励须联合守恒，不能忽略原蓝费

两支32局计划在第一实际episode1136、seed1433277931、task9b91c905…停止，未作新的PPO更新。完整110步/失败JSON/stdout/stderr保留，不能把计划32局算运行。step5原魔力汲取strong选Gold骰转White，原蓝费1；蓝魔力强化预选在实际付款后给Blue晶1，Blue3→支付2→获得3，最终不减少不代表免费。原增强效果White标记2、Gold效果骰移出与既有Green held/token保持。

独立核原JSON文本：basic009蓝费；00309 side1实际支付基本色后获得同色晶并发布该色中央标记。原ManaPool.TryPayExactColors在实际commit后OnManaPaid奖励，原Gain满3第四颗变同色临时标记。旧conversion auditor只算费和效果token，没有把实际共享奖励加回，因此该合法动作被误报。没有改原规则、费用或mask。

新转换oracle仅在本人未用轮标记→实际已用、arm色匹配原付费基本色、原费未豁免、明确唯一相同owner/color中央标记真实发布并清arm时计恰1奖励晶；将实际费、奖励及原上限溢出共同核算，其他颜色/两White产物/效果与付款骰仍严格检查。保旧auditor源码与旧失败，不能靠接受任意资源变化放宽预期。

实际重放110步before/完整action/合法性/after/raw完全相同，新2543检查零错误。原同入口无arm付蓝晶3→2、带Blue token支付且满Blue晶奖励产生Blue token溢出两个正式对照通过。6破坏例必须拒绝：少奖励、多溢出、错误中央色/owner、少效果White、费用豁免却激活。证据 `AutomationOutputs/BattleRl/20261008_shared_conversion_audit_failure/verification.json`。Python编译通过，Unity Console仍pending。

确认旧两进程自然退出后从上一次真实1136/64358/Adam345保存点恢复各32局，PID252076/279196创建2026-10-08T19:24:16.516968/19:24:16.913822+08:00，命令独立保存。不是观察超时重启，失败后的临时RNG不当已保存进度。活源不热换，原图/UI/全范围继续未达。
