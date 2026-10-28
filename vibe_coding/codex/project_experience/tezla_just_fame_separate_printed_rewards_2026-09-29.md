# 官方Just Fame须独立于印刷名望与能力

- Tezla p3允许不用阵营tokens而在击败阵营敌时额外+1；明确配置与界面标注，不把简化选项说成完整token实现。
- Monster.Faction为独立身份，不塞Ability、不改Fame，洞悉能力移除不影响身份。Loader复用已核JSON元数据，不猜其它敌人。
- EnemyRewardRules统一远程/近战/格挡消灭/直接消灭，EnemyRewards按实际引用记录一次，正式BeginBattle清本战记录，战后保留观察。旅程新Context保持模式但不重发旧奖励。
- 六牌场景原利斧强化击杀+1和阵营+1来源不同，正确35→40→42；全挡/原魔晶费用/同六牌跨轮仍保留。
- 敌人双甲行加入额外奖励会挤出能力行；实际ui.truncated拦截，保留失败图后只缩短文案，不删能力、不放宽检查。
- 正确区分历史printed-only模式的有效报告与当前Demo显式JustFame，不改写旧报告也不重测全库。

证据：vibe_coding/codex/pve_faction_fame_acceptance_index.json。后台四种kill均验；场景仅实际蝠近战与持续奖励，不声称所有途径场景。
