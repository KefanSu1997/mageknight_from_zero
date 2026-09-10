# 原版基础卡：选项、费用、目标格挡与魔晶

1. 原图存在不代表数据完整。本能原图清晰，但JSON/SO四个关键字段为空且映射Unknown。要同步恢复两种数据并在Unity加载实际SO测试，保留GUID和原图；重放脚本为tools/restore_instinct_card.py。
2. 原卡“或”必须建模为互斥分支。大地之子的治疗和格挡不额外产生移动力；按现有选项编号补充移动分支，避免破坏旧输入。强效格挡按当前昼夜、未修正地形费用，山脉5/湖泊2。
3. 验证应发生在支付前：ICardEffectValidator由ActionSystem调用，模块直接执行也调用它，防止非法选项先扣费、随后失败。该接口尚未覆盖所有旧效果，不等于全事务系统完成。
4. 寒冰护体的额外冰格挡必须绑定贡献并在实际目标确定后计算，不能在出牌时把某个夹具敌人的加成永久写进通用池。解析贡献后清除标记，避免CardCombatActions与BattleResolver重复加值。
5. 英文Cold Toughness说明PvP加成来自对手支付的魔力；中文卡文字容易误读。本轮只实现并验收敌人标记分支，PvP仍明确待联验。
6. 官方规则书第5页“Gain Effects”：同色魔晶最多储存3，额外奖励改为同色魔力标记。共用ManaPool入口处理批量跨上限奖励，场景验证满库存全神贯注与四张投射牌。
7. 验收保留旧350例的输入、选项和全部数值预期，只更新本能过时的缺数据说明，仍保留整卡生命周期待联验。

官方规则书（WizKids产品页Download Rules链接）：
https://eadn-wc03-13179282.nxedge.io/posters/repository/wizkids/MKUE%20Rulebook%20BOOKLET.pdf

详细运行目录和结论见vibe_coding/codex/worklog_2026-09-10_original_basic_card_rules.md。
