# 官方 Tezla Just Fame 可配置奖励

状态：实现与独立测试就绪，尚未执行后台/Unity，主代理负责串行执行及UI。用户可选偏好未答时暂采用官方p3推荐的简化奖励模式；未实现或宣称完整派系token系统。

## 规则与最小改动

依据本地官方 `tmp/pdfs/tezla_official_rules.pdf` p3 Just Fame：击败派系敌人时用额外1名望替代派系token。Monster印刷Fame保持不变。Monster尾增 EnemyFaction Faction 默认None；正式Loader读取已有独立faction元数据。现有015/054/057可读为Elementalist；不猜测其他原敌派系，不改任何JSON。本轮没有把阵营作为Ability，因此洞悉/能力移除不移除奖励来源。

明确模式 `EnemyRewardMode.PrintedFameOnly` 为历史默认，`TezlaJustFame` 显式启用。旅程尾部可选构造参数 `enemyRewardMode` 默认前者，保存会话配置并传入每个正式回合新ActionContext；独立战斗尾部 nullable 参数默认null保留既有Context设置。仅后台默认模式保持历史分数；主代理已为当前Demo显式启用TezlaJustFame，因此旧场景报告保留历史规则语境，后续涉及015/054/057的分数须按新模式核对，不反复全量重跑。

共享 `EnemyRewardRules`：`GetFactionFame(Monster,ActionContext)` 和 `PreviewBonus(Monster,EnemyRewardMode)` 纯预览，实际发奖集中经过内部TakeBonus。三个正式kill链路均接入：RangedPhaseResolver远程、BattleResolver近战/成功格挡消灭（含共用大师消灭）、CardCombatActions.DestroyEnemy直接消灭。实际攻击/格挡战果Fame含额外奖励，原印刷值独立不变。

`Context.EnemyRewards.Grants`只读记录实际Enemy引用、Fame、EnemyDefeatSource（Ranged/Melee/BlockEffect/DirectEffect）；Count/TotalFame可观察。每个真实敌人引用单战只发一次，已击败索引拒绝重复击杀；新战斗正式BeginBattle清记录，战后不清以便结果观察。正式旅程下一回合建新Context保留模式而不重发旧奖励，已击败真实标记仍不能再次BeginBattle。这样兼容地下城真实标记归回供应后未来合法再抽的情况，不永久禁止同物理标记在未来新战斗获奖。

## 独立后台范围（待主代理执行）

类 `MK.Tests.Battles.TezlaJustFameRewardTests`，仅4条：

1. `OriginalFactionKillsUseRangedMeleeAndDirectPathsExactlyOnce`：原015远程、原057直接消灭、原054近战，共3个派系奖励，原印刷3/4/1未变；记录来源，重复消灭拒绝，战果实际含奖励。
2. `SuccessfulBlockDestructionAlsoGrantsOfficialFactionBonusOnce`：正式成功格挡消灭路径实际+1，重入结算不重发。
3. `DefaultModeAndNonFactionEnemyKeepPrintedRewardsAndIdentityIsNotRemovableAbility`：默认原分数、无派系不加、能力列表与派系身份分离。
4. `JustFameOriginalSixCardBattleAndNextFormalRoundKeepSamePlayerWithoutOldRewardReplay`：复用原六卡7远程击杀两032→双槽实际格挡057→骑士精神基础攻击，原牌利斧奖励+1与蝠派系+1分别结算，35→40→42；仅六张原牌（Lv6手限6），空手空库正式AnnounceRoundEnd/EndRound/PrepareRound/night_1/BeginTurn；同六原牌引用洗回手，玩家/牌实体/伤牌/晶体不重建，旧敌拒绝重复战斗与领奖。

未运行CLI/Unity，未改UI、场景、共享JSON、生成器或fixture。完整派系token模式和其全部敌人目录不包含在本次有限奖励实现内。

运行前审阅纠正：移除不合法的第七张起手行军及无关移动消费者，改为正式跨轮验证六卡回手和名望42持久；方法名同步NextFormalRound。仅此测试改变，生产未改，未执行CLI。

## 21:15实际验收

主代理将独立战斗及连续旅程显式设为TezlaJustFame，顶栏及旅程提示明确官方模式，Numbers使用共用预览函数显示印刷名望+额外1，观察真实奖励Count/TotalFame。原怪物印刷数值及JSON不改。

CLI初编译发现测试已移除地图却遗留using导致两个Monster歧义，仅删无用using后210703_b366fb02四项全部通过（62断言）。Unity编译与最终Console均实读双零。

首次场景210916在24发现Numbers换行截断；保留原失败图，精简名望加号及双甲括号冗字，未改变数值/能力/断言标准。211247一次通过65重放步骤，仅6个新增检查30断言、3图均主代理目视。后台其他击杀途径并不冒称本场景全部实际操作；场景明确为蝠近战奖励及正式跨轮。

已有Elusive主路线生成器最终名望更新为42以匹配当前Demo显式模式；此前41历史证据不改写，不因此重测已过全库。完整阵营token未实现，不冒充原奖励道具全覆盖。

剩余原后续乡村/Core资料及普通头环混乱解释：本轮头环只做两条官方限定搜索，没有新的专项裁定，记录headband_chaos_bounded_source_audit_2026-09-29.md；无新来源不要重复相同查询。全程无push、无截图/日志上传、无VPN操作、无重启。
