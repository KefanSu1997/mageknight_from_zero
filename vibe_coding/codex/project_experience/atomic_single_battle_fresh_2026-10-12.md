# 完整操作与严格单场：从随机参数重训

用户明确要求合并UI预选并修单场终止后从0训练。旧 tail_safe_100 停于有效80更新，原模型/轨迹/未完成eval80保留，superseded_atomic_fresh.json记录。不能恢复旧80权重冒充新模型。

## 接口

JourneyTraining.Atomic.cs在原规则真实检查点上枚举完整操作，再恢复状态及RNG；不是替代规则模拟器。unitDamage按目标/攻击分组组合完整部队有序分配后Advance，一次结算整个承伤阶段；不能给一个目标选完就提前结算其他目标。补齐第四实际部队槽的有序组合。付款/共享强化选色必须接上真实消费，pending payment/enhancement不得跨策略边界。防御分配按实际攻击目标组合，弓模式只针对实际消费来源，击杀颜色只针对攻击目标。强韧旗开关绑定承伤，源骰归还/防护清理绑定正式EndTurn。

原子协议不提供取消/清空/预选作为策略动作。source/extraSource、阶段放弃、格挡等实际操作保留。必须等待新信息的共享后续解析仍是有意义决策。单场正式战斗End后只开放收尾，正式EndTurn及原升级待办完成后判胜负，不开始下一回合，不再休息/再次fight；因此没有跨入轮末再开轮的roundReturn菜单。并非宣称完整地图/轮末UI已验收。

训练环境与固定评估共用 atomic_battle_env.py。完整操作动态支持集存在不可变、追加写入的 atomic_supports.jsonl；rollout存support_id和局部完整操作ID，不能用旧全局ID解读。策略按完整支持集做categorical，参数描述与真实消费者共同评分，不做top-k。所有内部before/action/after在micro_trace留存，规则审计仍逐原操作进行。超出声明动作空间或接口失败明确隔离，不能静默裁剪支持集。支持集过大的组合仍可能有成本，尚未证明所有5000任务均已访问。

128策略动作上限指完整操作，不是内部点击数量。单坏局保全隔离后补采；正常局不丢。评估异常计失败，保留1000分母。固定.01 reward、完整局gamma/lambda1、SB3-Contrib上游RecurrentPPO和singleton尾批修复保持。

## 验证与边界

- 独立适配器构建 H:/MageKnight_RL_Runs/atomic_adapter_build，0编译错误/警告，未覆盖旧运行DLL。
- atomic_gate_final/gate.json：7英雄正式fight开始、原承伤与收尾，均恰完成一战；重复完成计数负例被审计捕获。CUDA真实8转移使LSTM/argument encoder等参数变化且有限；仅集成门禁，不是128局生产训练或胜率证据。
- atomic_consumer_gate.json：实际付款材料+强化出牌95检查、源骰归还+EndTurn199检查，无错误。
- atomic_enhancement_gate.json：实际共享强化+付费出牌49检查，无错误。
- atomic_defense_gate.json：原任务构筑经正式reset，在两种原Defend敌人的受控夹具中正式出牌/部队生成攻击，防御+攻击164检查无错误。不是独立策略获胜或原数据集性能。
- 旧预选循环上下文没有可执行消费者时，复合支持中没有该预选；不能据此误判为删掉已可用的能力。
- 防护清理/弓模式/击杀色/强韧分支已接入，但本次定向真实轨迹门禁没有逐一完成所有原卡组合；不能称全规则或Unity验收。

新的终止审计分支只接受明确formal_single_battle_v1记录，并严格检查敌人实体/败亡标记不变、completedBattles与completedTurns恰+1、相同轮次与回合、End阶段与未胜标记；旧跨轮断言未放宽。

## 新运行

### 防御支持集修正后的当前运行（覆盖下方首次启动）

首次partial baseline期间静态复核发现：多防御来源、少攻击目标时，只枚举最小来源会漏合法分配。已停止154864及其本任务规则子进程，零PPO更新，partial baseline与superseded_defense_support.json保留，不当完整0步基线。改为递增来源顺序枚举所有可选来源组合；atomic_defense_support_gate.json实际检查两原Defend来源针对同一目标均存在且分别原规则执行/审计通过。

当前2026-10-12 21:55:54本地 launcher158944 / Python245160：
H:/MageKnight_RL_Runs/20261012_atomic_single_v2_fresh100，fresh，无旧参数。DLL固定在atomic_adapter_v2_frozen/BattleRl.dll，SHA256=7b4d133bf417789aeabaa43e13ee21c08f58cb5c120480636c84ea2ebd1d0c39。新任务绑定重新生成；557源文件与数据集封存在该run/source_snapshot.zip。日志atomic_v2_stdout.log/atomic_v2_stderr.log，页面观察器launcher297132。先新的0步1000验证，不能复用漏支持版本partial baseline。

下方216局证据是首次版本的有限检查，不能当当前v2基线或证明其防御支持完整。

2026-10-12 21:47:21本地启动 launcher322492 / Python154864：
H:/MageKnight_RL_Runs/20261012_atomic_single_fresh100。
train_sb3_atomic.py，fresh随机参数，无resume。32环境、128有效完整局/更新、每局最多128完整操作、minibatch128、100更新。新的4000/1000池只重绑新适配器DLL/catalog，保留父task ID/定义/initial SHA，实际每次reset仍核初始状态；不创造可解证书，不改变组成隔离。

默认先0步冻结1000局，再每20更新全量验证。当前已初始化initial_checkpoint/initial_rng并进入eval0000；本记录不声称已经完成任何PPO更新。首批审查216完整基线局、2647策略操作/2752原规则操作，无独立预选/休息/fight、无审计错误，first_episode_gate.json保留具体文件列表。11胜为初始随机策略样本，不能称学习提升。

另发现原5000池中含防护旌旗的178训练定义，在这次逐项reset检查中均不在初始手牌。原构筑定义/牌序未在本次额外修改；这是后续任务曝光分析的缺口，不能把这些任务入池视为神器已被训练覆盖。

仪表盘atomic_dashboard.py观察实际PID句柄并每5秒展示baseline进度，使用atomic_tracking_page.py，明确纵轴及128完整局合同。stderr为H:/MageKnight_RL_Runs/atomic_single_stderr.log。活进程不重复启动、不热改。云备份仍只完成40分卷大小核对，下载SHA待办，H归档不可删。
