# PPO 物理伤牌成本修复

## 实际问题与修复

新真实PPO seed9354 step39换回合，旧伤实体只是移动：手伤4→2、活跃伤牌仍6，旧奖励却+0.03。源 `PlayerState.Wounds` 是手伤而非总伤；原代价还漏掉直接进弃牌堆的毒伤。保存原seed/动作/SHA及前后状态，不修改正式规则或旧报告。

`JourneyTraining.cs` 增加 ActiveWounds：按引用统计所有个人牌区、排除已移除区、物理实例去重。奖励成本改为该计数的净变化；旧伤重新抽到、弃掉、回洗均不算治疗/新增伤害，真实去伤才减少成本。状态追加 totalWounds、actor观察末尾追加总量，旧观察前缀保留。手伤字段继续保留原游戏含义。冻结v28独立DLL，不覆盖活训练v27。

初稿排除removed时误用 record 值相等的 Contains；第二条真实重放在首张伤牌移除后发现把其余同名伤牌也排除。保存失败后改为 ReferenceEquals；不能以伤牌ID相同替代物理身份。

`train_ppo.py` 评估保留 mean_wounds 兼容旧手伤口径，同时明确 mean_hand_wounds / mean_total_active_wounds；旧版本可从实际个人牌区计算总量。`trajectory_audit.py`独立核对 totalWounds 与真实各牌区实例的活跃伤牌计数，错误总量必须失败。

## 定向验证

- `verify_physical_wound_cost.py` seed9354完整60操作，所有游戏状态/合法操作保持一致；独立奖励公式为旧reward减0.02×(物理总量变化-手伤变化)。63项检查通过，移位奖励+0.03→-0.01。
- 新真实seed11216完整107操作。step62既新增伤牌又移除一张，成本按真实净伤牌增加计算，奖励-0.09→-0.11；不是看到removed增加就假定总伤降低。110项检查通过；终局手伤5、活跃总伤32，证明两项指标不可混称。
- 两条原轨迹SHA、完整新重放与独立规则报告分别保存在 `AutomationOutputs/BattleRl/20261003_physical_wound_cost_replay.json`、`20261003_physical_wound_actual_removal_replay.json`。负例篡改总伤计数被拒绝。指标检查复用真实终局观察，不重新跑一局或把统计校验算作策略胜率。
- v28编译零警告/错误；Unity Console按1000条错误与堆栈两次零错误：`tmp/rl_physical_wound_cost_final_console_20261003.json`。无Assets或场景改动；最终Unity策略回放仍待验收。

## 继续训练

旧v27 session72720 exit0、CIM已无训练进程，最终模型与nextcursor11516实际存在。启动 `20261003_physical_wound_cost_ppo`：8192步、七样本评估、window28、类型平衡、原开局，冻结 `tmp/battle_rl_v28_wound_cost/BattleRl.dll`，session12932；从v27最终policy_ppo.npz续训。旧Adam状态未保存/恢复，仍如实记录。当前只代表启动，修复后的性能与所有原要素/消费者/未见构筑及Unity验收不提前通过。
