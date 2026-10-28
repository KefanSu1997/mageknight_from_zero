# 原格挡贡献分选与PPO续训

## 原因与实现

实际seed73181模型生成领导增强物理格挡6却advance弃用；进一步核查正式PveBattleSession.Block只使用全部AvailableBlocks，RL也只有target/attack而无来源选择。按通用战斗测试目标补齐来源入口，不屏蔽合法advance、不修改原费用/格挡强度。

PveBattleSession新增BlockSelectedPreview/BlockSelected，旧Block保持全可用来源行为。选择必须为当前实际攻击可用对象引用、非空、不重复；复制同值对象、已消费对象、错误目标/分次组均拒绝。预览按原目标元素/迅捷/符号计算，只在整体足够后预留和消费选中的真实来源，未选对象保留；失败不部分消费。

JourneyTraining增加blockSelected，Parts是当前全局真实格挡列表前8槽的位掩码，目标及攻击槽独立；正式入口再次验证实际引用及组限制。前8以外来源目前仍可用旧all入口，逐笔选择范围缺口保留，不说全量无界。新目录92664动作（原86544完整Key/字段全部保留）、观察5670坐标与能力schema完全相同；无观察热迁移。恢复映射原动作与kind标签，新kind/动作初始化由现有PPO恢复流程处理，不将分布变化称为策略提升。

trajectory_audit核对blockSelected的实际来源位索引，只有选中来源消失，剩余数量/来源指纹保留；新动作也参与原技能格挡消费者判断。块观察仍缺单位来源元数据，实际轨迹需结合正式激活前缀关联，显式保留缺口。UI目前仍旧all格挡，不声称场景分选完成；下一步须接UI操作及保存策略真实回放。

## 后台与实际前缀验证

RulesCli、BattleRl各构建零警告零错误。新增原双决心强格挡两原032：分别消费5点，同名另卡物理来源引用保留后用于第二敌，两次完整格挡结算无伤；重复、同值clone、旧来源、2点不足拒绝且来源/预留状态不变。相关PveBattleSessionTests+BasicBlockRewardStateTests15/15通过，AutomationOutputs/RulesState/20261004_154430_fe3cccc1。

首次扩大检查45项36过9失败，其中新用例误用决心基础attack分支，纠正为原block选择后通过；另8条大师/守护者失败原样保留。用修改前冻结RulesCli overload_end_preview_v49对照同两类30项22过8失败，同8案例，证明已有失败仍存在，不能把整个45批声称通过，未放宽任何规则/旧预期。失败报告20261004_154136_bb9c988f及基线20261004_154318_8611d161。

真实seed73181前51操作在新DLL逐before字典一致；此后另做非训练预检，选择blockSelected:::1:2:0，正式消耗领导那笔物理6，保留原冰3和横置物理1，只有真实召唤物044攻击槽0被挡，单位/费用不变。1265通用审计零错误零gap，20261004_v50_selected_leadership_block_preflight.json。此反事实补验不是PPO使用、原训练消费者或Unity通过。错来源、未选来源丢失、选中来源未消费三个负例均触发新断言，20261004_v50_selected_block_negative_audit.json。

Unity两次refresh超时，端口153276仍活，未rapid retry/重启；按电脑操作技能选择当前Unity窗口，第一次遮挡截图内容非Unity未据此点击，激活实际返回Unity窗口后看到Console0error且编辑器有响应。重新refresh idle，Consolecount1000/stacktrue两次0错误，tmp/rl_selected_blocks_console_20261004.json。没有场景配置更改，未把后台分选当场景通过。

## 训练终态与新进程

v49 PID238408实际结束，8192步134局2210.89秒3.705步/秒，最终保存模型评价0/7完整胜利、1击杀局，Wolfhawk固定seed123456回放40操作零击杀。失败不删除，最终模型保留。

确认无活train_ppo后15:59:07启动v50 PID240872：16384步，恢复v49实际最终模型，seed接实际next_cursor75225，gamma.995/GAE.99、学习率.0004、探索系数.05，28候选原构筑、原开局/中期及60秒新实际轨迹审查。旧/新冻结DLL计数独立，不绕过继承守卫。输出20261004_v50_selected_blocks_entropy005_ppo；本轮未产生策略效果结论，后续须核实实际PID命令行、启动时间与输出更新。完整地图场地、全部技能/原模式/消费者、未见构筑评价及保存模型Unity仍未完成。


## v50 新实际训练轨迹增量审查

PID 240872 确认仍存活，未重启。新 seed75473 的90步实际轨迹通用守恒检查2103项通过；seed75347的58步1400项通过，但整段未尝试战斗，保留 unsuitable as combat tester 缺口。证据分别为 `AutomationOutputs/BattleRl/20261004_v50_first_actual_unit_attack_consumer_audit.json` 和 `AutomationOutputs/BattleRl/20261004_v50_first_actual_supply_acquisition_audit.json`。

对照原凶蛮僧侣与投石车卡面，seed75473实际step5-8：白昼金标记支付僧侣原绿费，物理攻城4击败甲4目标；投石车原红费支付一个实际红标记，火焰攻城5击败甲5物/冰抗目标。两原部队耗竭、付款和确切来源消费共13项定向断言通过，见 `AutomationOutputs/BattleRl/20261004_v50_actual_two_unit_consumers_review.json`。这仅证明真实局部消费者；不代表整场胜利、阵营奖励或Unity操作验收。未修改代码，不触发编译回归。


### seed75347 原远古血脉取得及跨轮复用

原卡面核对高级030基础：一点伤牌与一点同色魔力，从供应取得高级009冰霜之桥。真实step17蓝晶-1、伤牌+1、原供应补位，同一物理牌258入手；step19与32分别白昼第一轮、黑夜第二轮免费基础移动+2，并从实际手牌进入已打出区。全58步前后状态连续。共11项定向断言通过，证据 `AutomationOutputs/BattleRl/20261004_v50_actual_bloodlines_cross_round_card_review.json`。没有地图沼泽减费消费者，整段无战斗，明确保留缺口；不把取得、打出或跨轮复用冒充完整原卡验证。


### 首1024步定时审查与新真实停滞前缀

实际stdout保存1024步/16episode，9246项通用检查零错误但3个缺口，不等同全部原规则通过。新seed75644末尾战斗结束后实际手牌16张全为伤牌，no_legal_continuation。以同冻结v50 DLL、原开局配置逐46步重放，92个完整前后state完全一致，done=true、won=false、legal=[]；保存 `AutomationOutputs/BattleRl/20261004_v50_seed75644_dead_end_replay.json`。正式恢复入口与具体拒绝原因仍待核查，不放宽最低行动义务、不强制替策略行动、不算胜利。仅关闭此次创建的独立短程诊断规则进程；活训练PID240872未终止。


### 全伤牌停滞根因与正式恢复分叉

阅读本地原规则PDF第6页玩家回合5a、7a和第11页7a/创伤与治疗1a：至少打出/弃一牌义务，全伤手牌缓慢恢复弃一伤，回合末不能任意弃伤。实际seed75644第二回合承伤已弃非伤牌且activity=1，第三回合真实缓慢恢复后正常结束；第四回合step38全伤牌activity=0却选择fight，非承伤弃牌漏记。重放前38步76个全state比较一致；在原分叉选择正式rest，再正式nextTurn实际进入第5回合，81项检查通过。证据 `AutomationOutputs/BattleRl/20261004_v50_seed75644_formal_recovery_branch.json`。这是可行替代动作证据，不是已训练策略作出该选择，不改写原失败，不放宽回合义务，也不因存在伤手就禁止可能通过技能/部队抽牌满足义务的所有战斗。训练继续运行。
