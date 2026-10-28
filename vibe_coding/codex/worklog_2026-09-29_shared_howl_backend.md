# 群狼嚎叫004:09后台接入（2026-09-29）

## 原卡与实现
已读 tmp/shared_skills_rules_audit_20260929.md，并实际查看 skill_en_015.png 最后一行。交涉外真实原手牌横置4，每个无单位分配的指挥标记+1，成功后原技能入中央。首位进入敌战的玩家（包含owner）强制归还并各选护甲-1、一次攻击-1；后者可为同或另一实际敌人。

本代理独占新增 SharedSkillArea.Howl.cs 与 SharedHowlSkillTests.cs。复用Partial主类的实际GameEngine校验、Publish/Return及原卡横置入口；不造通用免费资源。取player.FreeSlots，已核普通槽、空忠诚槽、指挥旗以及耗竭部队仍占用槽的行为。

正式战斗开始仅快照领取当时已有Howl；战内发布不倒触发本场。领取后建立绑定实际context与player的队列，并保持发布顺序。每次Resolve只消费队首；双目标/攻击槽全部有效才修改真实SkillArmorReduction和SkillAttackAdjustment。护甲最低1与攻击最低0复用正式消费者。

## 公共API与外部钩子
- UseHowl(player,context,skill,actualCard,SidewaysType) 返回真实SharedSkillToken。
- OnBattleStarted(player,context)：正式实际敌战开始事件只调用一次，必须在真实敌列表绑定且进入Ranged后；不能在战内Publish时调用。
- PendingHowls(context)：只读队列快照。
- RequireHowlResolved(context)：未选完阻塞其他操作、阶段及回合结束。
- ResolveHowl(player,context,armorEnemy,attackEnemy,attackIndex=0)：实际当前玩家和实际目标选择，失败不改减值、不移队首。

Root负责RepeatedEffectResolution、TurnMachine、GameEngine及UI接入。Resolve绕过共用RequireActual内部的待选守卫，直接核真实回合+未完成重复卡，避免“待选阻塞自己的选择”自锁。

## 新测试范围
7个新增case，未运行CLI/Unity，不声称后台或场景通过：
1. 实际手牌横置、耗竭部队仍占槽、原技能实体发布。
2. 真实忠诚取得与指挥旗免费招募后空槽数，不直接写internal setter。
3. 交涉、虚假材料、非法阶段的拒绝守恒。
4. 真实双玩家轮次后另一玩家首战强制领取，阻塞普通出牌/推进阶段；非法目标不消费；分离目标真实减甲击杀与减攻。
5. 本人也可首位领取，战内发布不倒触发；使用同回合二战控制器仅检验技能生命周期，不宣称旅程可执行第二个独立行动。
6. 多个物理Howl实体按发布顺序处理（独立供应压力夹具；不宣称标准单套游戏有多个相同原token）。
7. 真实攻击槽与护甲/攻击最低值。

未提交、未推送、未操作Unity，等待主代理串行验证。
