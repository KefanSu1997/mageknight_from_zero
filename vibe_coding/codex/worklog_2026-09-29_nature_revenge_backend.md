# 自然复仇／笨重消费者（2026-09-29）

## 原规则依据
- 已读取原图 Assets/GameData/cards/skill_en_004.png 最后一行：每轮一次，一个实际敌人一次攻击-1，整个敌人本回合获得笨重；中央共享标记允许本人下回合前另一玩家归还使用同效果。
- 正式 Lost Legion 规则：格挡阶段每支付1移动使笨重敌人的一次攻击降低1，影响该攻击后续承伤；减为0算成功格挡；不能用移动阶段剩余移动。
- 官方规则PDF检索索引： https://www.mageknight.net/wp-content/uploads/Mage-Knight-Board-Game-Expansion-The-Lost-Legion-Rules-2013.pdf 。网页直开遇验证，检索结果有完整Cumbersome段，未下载PDF。
- WizKids参考表再次一致确认： https://eadn-wc03-13179282.nxedge.io/mkmaster/MK%20Master%20Reference%20Sheet.pdf 。原文13词：“An Attack reduced to 0 is considered to be successfully blocked.”

## 最小实现与分工
本代理只新增 NatureRevengeActions.cs 和 PveNatureRevengeTests.cs，不修改共享状态、引擎、UI。
独立State保存实际Monster引用的笨重目标与每攻击槽减攻；Grant做原-1，SpendMove整体验证后实际扣池并减攻。零攻击记录成功格挡，不消费其它格挡贡献、不自行额外发奖。ClearBattle仅清本场Spend减值，保留原Grant的本回合-1与笨重；ClearTurn全清本模块，不修改其他技能减攻。
主代理接 ActionContext.NatureRevenge、DefenseProfile最终减Reduction、正式战斗与回合生命周期清理，以及原技能/中央共享交易与UI。

## 准确API
- ValidateGrant(ActionContext, Monster, int attackIndex=0)、Grant同参。
- ValidateSpendMove(ActionContext, Monster, int attackIndex, int points)、SpendMove同参。
- HasCumbersome(ActionContext,Monster)、Reduction(ActionContext,Monster,int attackIndex)、ClearBattle(ActionContext)。
- NatureRevengeBattleState.CumbersomeTargets只读。

## 验证与保留缺口
12个新测试case已写，等待主代理串行CLI；未运行Unity，未声称后台或场景通过。测试通过战内正式原行进出牌取得移动，然后校验正式DefenseProfile/伤害消费者、多攻击、同名实例隔离、0成功格挡和失败守恒。
共享入战入口目前尚无明确移动阶段残余移动清理；这属于共享规则影响，主代理指示本批先显式保留缺口，后续连同UI处理。当前辅助API按传入的战内MovementPool消费，不能据此声称旧移动来源已经隔离。
中央共享标记的拥有者、时限、领取次数由主代理实现，本辅助不伪造多人共享完成。

## 本回合期限与行动边界补核
已将Grant和Spend分别存储。生命周期测试改为正式EndBattle→同context同enemy再BeginBattle→正式EndTurn，核Grant保留而Spend清除、回合末Grant失效；未运行。根代理接ClearTurn调用。正式规则链接与迷宫/灵动例外边界详细记于tmp/nature_revenge_movement_boundary_audit_20260929.md。
