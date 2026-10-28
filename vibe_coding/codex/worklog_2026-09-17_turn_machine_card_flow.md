# 原卡贯穿正式战斗调度器

## 最终本批验收

7项后台与6个真实场景均符合。火球4例116项断言取 `AutomationOutputs/AllOriginalCards/20260917_020704_c0af960e/magic`；原盾2例48项最终取 `20260917_021043_336ff138/items`。两个完整UI报告success。已查看原盾最终两张截图，顶部已打出0与下方实际/预期一致，基础弃牌3/3、焚毁移除1/1，阶段End、战斗False、下一场伤牌+1完整可见。共164项效果断言；仅本批6例退出日常队列，旧回归未全量重跑。

Unity Scripts最终02:09:36；修改后Console两次0，最后场景后再次两次0。所改文件git diff --check通过（仅LF换行提示）。历史992案例：398 passed / 586 partial / 8 failed；8例仍是缺有效原定义的advanced_card_044..047，无有效卡失败或缺失。本批6例保留整卡未覆盖的partial声明，目标active。无远端传输或系统/VPN操作。

后续优先：原元素格挡成功后生成攻击的分阶段消费。BattleResolver在格挡成功时向CombatPower添加AttackOnSuccess，但TurnMachine当前一次同时接收blocks和预先构建的attacks，调用方无法在奖励生成后再选择攻击目标；本轮收尾会清掉这些未被分配的奖励。需要以该原卡真实操作复现并接通分阶段选择，不能认为本次完整调度6例覆盖了它。另有敌人移除时按索引记录的减甲/跳过攻击/元素修改必须保持实际目标身份，继续待验。

上一轮属于实际进展。本轮查看真实调用链后发现：TurnMachine.ResolveBattle注释写结束但只调用一次NextPhase，实际从AssignDamage推进到Melee；它也没调用EndBattle。ApplyRanged未消费context中的远程/攻城贡献。GameState没有公开的战斗开始入口，外部只能看到初始None。

最小修复：BeginBattle只允许None/End进入Ranged；ApplyRanged在正式结算后消费该阶段全部远程/攻城贡献（含主动放弃未分配部分），再进Block；ResolveBattle已一次完成伤害和近战，因此成功后推进两个子阶段到End，调用既有EndBattle并同步context.CombatPhase。重复ResolveBattle被原阶段校验拒绝。没有重写战斗数学或复制卡牌效果。

后台同源NUnit `TurnMachineCardStateTests`：原火球基础/强化实际出牌、费用及自伤，远程不足不能带进近战/下场；原盾基础/焚毁真实实例、首场格挡与下一场承伤；远程击杀后空战斗结束不重复名望；错误阶段和非法部队分配不结束战斗或消耗现有格挡；主动放弃远程也不能留存攻击。

基线 `AutomationOutputs/RulesState/20260917_020145_f2399989` 1/6符合、5失败；修复后仅5失败例 `20260917_020211_eff09a83` 5/5符合；分配拒绝边界 `20260917_020356_ea995e7f` 1/1；新增主动放弃例 `20260917_020445_bda5e94d` 1/1。最终7项后台符合。

场景入口新增turnMachineStartPhase，仅对本批配置生效。TurnRanged、TurnResolve、TurnBegin调用正式TurnMachine，分配来自context真实攻击/格挡贡献；场景只选择目标并提交命令，不替代结算。按需记录GameState阶段、InBattle、实际击杀/伤牌、剩余远程及名望。按钮明确提示阶段操作，最终对照独立预期；自动化加ui:textFits断言。

新增6个场景：magic_009_10..13（不足基础/强化、击杀、主动放弃，再拒绝重复结算及跨场借攻击），items_020_18..19（原盾基础/焚毁连续两场及回合卡区）。`AutomationOutputs/AllOriginalCards/20260917_020704_c0af960e/magic` 4例116项效果断言及UI全符合，已查看强化、击杀和放弃截图，费用、原卡、伤牌和阶段完整可见。`20260917_020755_95a87e85/items` 2例48项效果断言及UI符合，但目视发现顶部仍显示EndTurn之前的已打出1，下方正确显示0；修复完成时同步顶部最终值，仅重新验证两个盾例。

边界仍需后续完成：TurnMachine既有敌人移除索引与持续目标效果如何同步、格挡触发的后续攻击如何在分阶段完整使用、其他上层Adventure的临时context及独立卡区适配。当前适配器只支持5张基础卡，不可把调度器的6个验收场景说成整个冒险已接通所有原卡。本批不扩大修改这些不同路径。
