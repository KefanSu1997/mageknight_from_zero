# PPO原生重整旗鼓目标入口

## 问题和必要修改
原卡hero_skill_pool_002:02“重整旗鼓”：每轮一次、战斗除外，重整或治疗一支实际部队。正式RallyEffect和PveJourneySession.UseOutsideSkill已实现；RL generic skill调用却丢掉TargetUnit，目录仅target0，故模型无法操作任何部队。只修tools/BattleRl/JourneyTraining.cs：该技能生成三实际部队槽，战外候选按真实自有部队数量，Apply传入所选实际UnitState；其他技能目标语义保留。原Assets规则和UI未修改，旧3部队容量缺口仍在。

trajectory_audit.py增加原本人每轮次数、战外时机、所选槽/其他单位不变、无牌/魔力/治疗点费用、重整不治伤及治疗实际去一伤。train_ppo.py留存两个分支首次真实PPO轨迹，激活与后续同单位消费仍分开审查；未放宽任何原规则。

## 验证
冻结DLL tmp/battle_rl_v34_native_rally/BattleRl.dll，dotnet --no-restore构建0警告0错误。verify_native_rally.py复用已训练PPO在实际正式环境到达状态，只用合法原操作：seed17重整第二支army_2_002后同部队真实发动；seed101治疗第三支army_2_014实际伤1→0，再正式轮转后同部队真实发动。450+1127检查0错误，其他部队不变。重复读取不变，本轮第二次发动不合法。把记录改成目标0，独立审计必须拒绝。初次运行已保存轨迹，再补审计只重读既有新证据，未重跑已通过分支。

Unity Console两次count1000含堆栈零error：tmp/rl_native_rally_console_20261004.json。新训练代码和审计Python无Unity程序集改动。本版本正式跨轮次数恢复、PPO实际使用、最终Unity操作和全部构筑泛化仍未验收。继续训练而非宣称完成。


## 兼容证据与续训

20261004_v34_puppet_prefix_review.json：新DLL按Key重放旧v33 seed33647傀儡PPO真实105次操作，全部前后状态和奖励完全相同，2210检查0错误。没有重新启动/重跑整个卡库。

确认旧trainer PID218924已终止后，新trainer PID214692于2026-10-04 02:59:03启动，续用v33 policy_ppo.npz，seed-start35631、8192步、factor-action-kinds、include-original-openings、28seed合法欠覆盖窗口、60秒新实际轨迹审查。输出AutomationOutputs/BattleRl/20261004_native_rally_ppo，冻结v34规则DLL，未覆盖旧训练规则。新原生Rally轨迹首次ready/heal各自保留，尚不提前声明PPO已经使用。

v33单轮去重快照：基础25/28、高级34/44、法术14/24、神器18/25实际效果ID，原部队29/30实际发动，本人技能50/70实际发动ID，69敌人遇见。没有合并不同版本计数伪造全面覆盖；本轮抽样覆盖不等于原规则、消费者和Unity验收。文件inventory_gap_snapshot.json可追溯。


## 跨轮恢复补验（沿用既有通过轨迹，仅补缺口）

20261004_native_rally_round.json：seed17重放原22次已通过操作后继续正式流程。op36 nextTurn进入轮2 Night，usedSkillsRound清空、相同army_2_002按正式轮末重整；op37同部队实际发动原能力2、再次耗竭；op38选择部队槽1发动本轮Rally，状态ready false→true且usedSkillsRound恢复该原技能。811检查0错误/缺口。确认每轮次数不是每回合刷新，不是重建玩家快照。属于定向后台证据，不是PPO实际发动或Unity验收。

活PPO PID214692：1024步10542检查0错误1保留缺口，2048步11713检查0错误0缺口。此时两个first_actual_native_rally文件尚未生成，不能把入口补齐和定向验证当作模型已使用该技能。
