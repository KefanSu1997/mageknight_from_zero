# Demo测试策略与rollout规则检查

用户2026-10-09明确目标：1.训练可用于demo测试的策略；2.利用该策略rollout检查各规则/卡牌正确性。前者要求保存模型在正式连续流程自主行动且提供Unity实际操作证据，后者要求原印刷文本/费用/物理实体/消费者逐项对应，不能用胜率或目录加载代替。

## 当前事实与本轮执行
- 开始时已核无活BattleRl训练/评估或Unity编辑器；此前各实验自然退出，无终止用户进程。最新承伤代价模型冻结4/99与无代价半LR4/99同数，父点6/99仍保留；33组成三actor流不是99独立泛化任务，30最终test未使用。
- 在结构/奖励微调的多轮短实验均未超过父点后，明确改为更长同目标训练的实验：从实际half_lr1168/66657/Adam361检查点继续96新局，243候选全局混合，LR.00005、MLP3/GRU/实体Transformer/共享动作参数、同terminal_sequence与旧共享规划成本保持；无新教师动作、无新规则、无放宽费用或合法mask。原32+后96构成同分支128新增局，不重复从父RNG采样首32。
- PID267024创建2026-10-09 01:44:19.601989+08:00，完整命令保存AutomationOutputs/BattleRl/20261009_demo_policy_half_lr_continuation_96_launch/process.json；活进程不热换或因观察超时重启。后续完成后冻结模型对照较好父点；续训分支未晋升、不继承其他失败分支计数。
- 复用已有真实half_lr32局2299步，生成demo_rollout_rule_review_baseline.json：53346已执行有限检查0errors；112不同原始card_mode键、48不同unit_invocation键、36不同skill_invocation键、30英雄敌对键，仅为实际发动下界（不是卡张数或完整消费者正确性）。每个首次事件绑定seed/task/原轨迹/step。没有重复全库或重跑旧审计。
- WorldFly6/Influence19/Move6，但仍无worldInteract/worldRecruit消费者；必须明确保持缺口。112原始mode键在当前native模式映射绑定之前不宣称规范模式总覆盖；卡牌印刷费用/材料/后续消费还须定向核查。

## 下一阶段的必要门禁
1. 核新96局真实进程输出和保存的原始动作前后状态；发生规则/费用审计异常先保留真实seed/动作与失败再修，未知任务不因为策略失败判无解。
2. 对新/失败/受影响rollout做原牌文本/费用、牌区实体守恒、攻击和格挡来源、伤牌/跨战斗/跨昼夜检查，按原ID/模式/费用材料/消费者建立证据及缺口；费用点数产生不算后续消费通过。
3. 完成训练后的冻结策略评估，包括完整declared任务和循环/规则曝光质量；重复开发集只作比较，不冒充最终test。
4. 使用通过规则检查的连续流程完成保存policy自主Unity点击。现Unity编辑器未开、原局部按钮映射不算自主完整rollout；最终必须取得实际Console两次零错误、场景原素材/操作/状态/结果证据。后台通过不能替代该门禁。
5. 目标全部原卡、7英雄69敌、技能部队神器的主要原费用/消费者保持；基本011/025来源未解决保持，不塞给原7英雄。最终完整目标尚未实现，巡检不得停用。


## 2026-10-09 续训实际新异常与修复
- 首查267024已退出，保存真实40新局10整任务，1208/68993/Adam381。1208失败79步在step68触发回合魔力audit异常，先保失败seed/action/旧源码，未重启/热换。
- 对照原place006实际卡图确认林地开始回合昼金1/夜黑1；实际坐标1,-3夜林地由旧Black1清除后新发Black1。修audit exact收入增加这条独立原规则，而非让旧token随意保留。79完整before/action/合法/after/raw正式重放相同、1820有限检查零错；69实际非林地对照Black1→0；6破坏例拒绝。原规则/DLL不变，Python编译通过。
- Unity进程未开、当前工具发现无Unity/Console API，无法取得两次Console零错误；保持场景/Console欠验，不把Python通过当Unity通过。新经验actual_glade_start_income_audit及AGENTS索引已写入。
- 只允许从真实1208/68993/381保存点续余56局，同架构/池/奖励/LR和完整恢复。原失败与未保存中间局不计训练，不能伪造96已完成。


## 当前规则数据绑定的缺口（不追认legacy）
- 使用现冻结world DLL只读反射导出96原行动/高级/法术的BattleCardChoice.For Enhanced模式，绑定DLL/原清单SHA，world_bound_frozen_original_modes保存；不是规则通过/新发动。
- 用已保存1208 checkpoint及其完全一致actual_training_events运行缺口统计，demo_policy_40_new_actual_gaps保存。总体历史201模式/68技能包含legacy，不能宣称当前全要素覆盖；当前data-bound仅126模式、30单位发动ID、52技能ID、76英雄敌人pair，缺116模式/18技能，0unresolved。单位ID发动不算所有能力/费用/消费者通过。
- 现aggregate排名含legacy缺口，另调用rank_drafts对current_data_bound_invocations排名，current_data_bound_task_priorities保存；只train组成，候选纸面gain不当可解/覆盖，后续课程必须独立真实见证与实际训练才计数。未改活trainer/source或池，243所有候选支持保持，未使用验证反馈移动任务。


## 新真实费用与后续攻击消费者证据
- 40新局逐条classify_attack_sources，只按正式attack.Parts选中来源与物理cardInstance回溯：9不同效果/目标消费者键、8横置键，0未知卡来源；demo_policy_40_attack_consumer_ledger保存，横置法术不当发动，未选part不计消费。
- episode1199/原seed/task：step48 magic015 base实际Blue晶1→0、其余晶/token不变，instance18手→已打出，产生Ranged Ice5；step51 Melee正式选择该part与其他来源，全部贡献消费并击败原monster039（原护甲6/城防/名望5，实际名望24→29）。原magic015图已视觉读蓝费1/远程寒冰5；这是base原费用+物理实体+后续消费证据，不当strong/所有组合/UI通过。actual_magic015_base_fee_and_consumer_review保存。
- 首恢复episode1208与自身原失败79完整保存trace/task精确相同，审计1820/0errors；glade_resumed_actual_prefix保存。PID159680仍活，不因等待或观察超时重启；后续保存/评估须继续核验。


## 2026-10-09 新目标缺口冻结探索与续训状态
- 先核159680创建02:12:37.876335及dotnet155108创建02:12:39.242449完整命令/输出持续更新，未重复训练/热换。已保存至1252/72179/Adam403（剩12计划新局），stderr空；这些中间训练结果不作冻结提升。
- 只读当前绑定缺口前两train构筑，保留全部原draft定义/split，仅selection队列改为Tovak a432aea8…、Wolfhawk76c6e881…，不使用validation反馈；用真实1136父保存策略/独立actor seed93301探索各一次，PID132192创建02:21:21.057360、完整命令/输出保存，已自然结束、stderr空。
- 两真实轨迹82/58步，检查1925/1375零错误、参数冻结0更新，两任务均未完整赢；Tovak策略后继死路不等于初始不可解，Wolfhawk空stop原因不猜无解。current_gap_policy_discovery_summary保存，未知仍隔离于胜利池，未改243活训练池或计训练覆盖。
- Wolfhawk新冻结rollout step38 items010烧毁：原卡图视觉核影响9/名望3，实际4→13/24→27、物理instance16手→removed而非played，晶/token不变；影响到step57nextTurn清零，未取得交涉消费者。actual_items010_burn_and_missing_consumer_review保存，burn资源/实体有限核验通过，normal/后续交涉/UI均欠；此是策略探索规则证据，绝不冒充新PPO发动覆盖。
- Unity编辑器未运行/本地60022未监听，Unity Hub仍在，但不能据Hub进程假装可取Console。实际两次Console和自主UI仍pending；仅缺少这部分不阻断已有后台训练与证据工作，当前目标继续active。


## 2026-10-09 96新增局已完成，冻结与完整原模式账本
- 首核159680/155108自然退出、stderr空，最终1264/73045/Adam409。同分支40+56共96新局6388步20完整任务、149121有限审计零错误；原1208失败与旧oracle保留，只从381实际保存点恢复，未重复计失败局。demo_policy_96_actual_training_summary保存。
- 全243池同源LR.00005/架构/原奖励保持；训练20/96不是冻结成绩，也不拿每更新局部计数替代整体。原较好父6/99仍保留，最终test未用。
- 冻结当前1264在33development组成各10301/10401一次，PID347048/250156创建02:32:46.374177/02:32:46.776596，完整命令已保存；最近核两活进程及各自dotnet实际命令/输出更新，无新异常，不重启/热换。10501待两者结束才启动，现不冒称全冻结结束。
- 当前data-bound发动144模式/58技能/107hero-enemy对，仅下界；最新缺口98模式/12技能继续保留，历史203/68不可追认。按原清单242卡模式建立demo_policy_rule_correctness_ledger，发动与费用/效果/实体/消费者/Unity分别记录；只挂本轮明确三张卡的有限证据，未链接旧证据不删除也不伪造失败。
- episode1211原学习base实际影响+2，31步付6取advanced013至弃牌，供应3→3补充、同新物理instance40后于40步真实打出，原卡图视觉核对。actual_learning_base_acquired_entity_consumer_review保存；strong/同回合二次拒绝负例/Unity与 acquired卡所有规则尚欠，不把局部资源链算整张卡通过。
- 完整目标仍active：策略可用性需冻结稳定与真实Unity连续自主操作；全部原卡规则和费用/材料/消费者仍需按实际缺口补齐，不能因新有限证据结束。


## 2026-10-09 1264冻结前两流与确定性解码诊断
- 347048自然退出、10301为1/33，60564有限检查零错/冻结0更新；250156首查仍活随后自然退出，10401为2/33、63321检查零错/冻结0更新。未因观察超时重启。两流暂3/66，没有完整99结论。first/second_frozen_summary保存。
- 最后10501启动345276创建02:43:38.204507，完整命令/输出保存；先前两支均退出后才开，不重复训练或hot换。
- 当前1264额外做一次完整33组成hierarchical_greedy确定性解码，PID/创建时间完整命令保存hierarchical_frozen_launch/process.json。模型同一权重，不改合法mask或原规则，不是新PPO学习；确定性每任务只一次，不重复同一任务。目标是诊断kind-then-parameter解码是否更适合连续测试，不能凭少数成功子集晋升。独立finaltest仍未使用。


## 1264完整采样冻结3/99与真实解码分歧
- 10501已自然退出、stderr空，为0/33。完整1+2+0=3/99、185572有限检查0errors、参数冻结0更新；较1136父6/99仍退化，不晋升。full_frozen_summary保存，20/96训练采样胜利与开发评估明确分开，finaltest未用。
- 仍活hierarchical227136及dotnet342692保持实际命令/创建时间，当前18任务0完整胜利，不能当全33结果。已保存0419…256步实际228unitDamage循环：step255 kind unitDamage .8056/16合法参数，具体选择概率.06794；同状态合法advance概率.19438、联合MAP会选advance。hierarchical_actual_damage_loop_review保存真实有序plan/选择证据，合法计划不判规则错，后继step_limit不判初始无解。
- 因新真实分歧，另一次同1264完整33 joint_map确定性冻结（每任务一次）启动，PID/创建/全命令保存jointmap_frozen_launch/process.json，不改模型/规则/mask或弃失败。用于确认离开预选是否伴随整任务改善，不能凭一个替代步骤说模型已可用。
- 为Unity Console/真实场景门禁发出缺编辑器+UnityMCP连接的用户输入请求；不是代码/部署批准。当前工具无UnityAPI、编辑器未运行、60022无监听，AGENTS明确要求UnityMCP Console两读；连接未恢复前不伪造该验收。后台工作仍有进展，目标保持active，未将其标blocked。


## 实际混合概率诊断与下一单变量实验
- 当前两确定性评估仍需等真实退出，不重复或终止。对真实1168/66657/Adam361父点重建同243原采样器，原skill floor .25后certified37.5%/unknown62.5%，不是最终各50%；world上下文29.477%。这只是明确原层叠概率，未改旧历史。
- 将skill floor .25→.05的纸面实际probabilities严格计算certified47.5%/unknown52.5%、world10.671%，全部243均>0且和1；不删未知、不把PPO失败判无解，不改旧原代价/规则/动作。skill_priority_mass_actual_diagnosis保存。
- 新96同源单变量采样实验具体定义skill_priority_mass_experiment_definition保存；对照复用已有同父原floor96真实轨迹，不再跑控制。尚未启动，需当前评估退出以避免同时3个大型actor进程；旧6/99较好模型和3/99失败均保留，是否改善须实测冻结，不能预判。


## 2026-10-09 两完整确定性结果与采样单变量实际启动
- hierarchical227136已自然退出、stderr空，33整组0成功、8309操作（7209 unitDamage）、198894有限检查0errors、冻结0更新；联合MAP99720随后自然退出、stderr空，33整组2成功、2246操作、51749检查0errors/冻结0更新。两summary保存，不因MAP缩短循环或两成功说结构学习/全可用；两完整失败不删，不重复确定性任务。
- 原1264样本3/99父6/99与二解码均不晋升，finaltest仍未用；单位有序预选228重复的合法性与策略失败分开。
- 确認以上进程实际退出后，新的同1168/66657/Adam361父点96局skill floor .25→.05单变量采样试验启动，PID339600创建03:12:35.455600，完整命令保存skill_priority05_global96_launch/process.json。仅任务概率变化、243全正支持，复用已有同父原floor96控制；原模型117数组/Adam/RNG/奖励/LR.00005规则数据/原胜利边界恢复，不伪造首次采样操作相等（采样器主动改分布）。experiment_definition标实际启动。
- 这个实验不是降低规则预期/删除未知或改变用户范围，原两世界技能消费者及全原费用/材料/消费者/UI缺口继续保留。Unity连接用户输入仍未收到，未依靠时间推断连接/批准；后台继续工作，goal active。


## 新skill mass .05实验真实检查与原神器消费者
- 首核339600创建03:12:35.455600及dotnet297192创建03:12:36.654550的完整命令/输出增长，未重启或热换。manifest显示唯一skill_task_sampling_change .25→.05、LR.00005/完整优化器恢复；源SHA187c27…精确指向1168父点，最初三更新真实12局3整任务不是96完成或冻结提升。最近保存至1252/72368/Adam403，stderr空。
- 新episode1181原德鲁伊法杖burn(11步)选择Blue/Red两项、无需另弃材料；本体真实removed，同色Red晶满3后实际token+2（这一步无新晶留库），实际耗竭level2 army2_012重整，15步同部队再发动。原卡图视觉核对，两Red token未实际费用消费、到次回合过期，保持消费者缺口。原亡君之盾burn(30步)多目标选项三ColdFire4/同distinctGroup，35步对monster002攻击5消耗其中1份与额外原卡贡献、余2份保留后37步失效；不冒称多个敌人或全部选项通过。staff_and_shield_consumer_review保存。
- 新episode1182原法术宝典burn(20步)弃原Red basic021物理12，借红法术006强力White分支；印刷Tome免原Red/Black费，本体17 removed，White原晶3导致3新实际伤牌，增2同色溢出token，晶与其他色tokens保持，没有造自有/临时magic006实体。原两卡图视觉读取，tome_radiation_material_and_wound_review保存；不当自有法术发动/多人或Day/UI通过。mode ledger_v2追加这些限定证据，1264基线发动旗标不回填当前 sibling实验。
- 对1264实际绑定历史进一步诊断：汇总seen68技能中10个仅legacy（through1103），当前只有58。legacy_priority_visibility_diagnosis保存。该事实说明总计数不能掩盖当前缺口，当前活实验未改历史/源或采样；后续若改优先视图须显式保持原117数组/Adam/RNG/历史并单变量检验，不能静默删除旧证据或热换。
- Unity连接请求尚无答复；没有从超时推断批准或连接。当前仍有训练/原规则证据进展，目标active，不将外部UI缺口当已完成或全任务blocked。


## 03:42 新采样96局真实完成与冻结启动
- 339600/297192已自然退出，stderr空。新96局6689步骤23完整任务，unknown实际52局，157132有限检查零错误；最终1264/73346/Adam409、skill floor .05。skill_priority05_training_summary保存，23/96不替代模型冻结表现，也不与旧20/96直接断言学习改善，按可解/未知组另分报skill_mass_training_by_feasibility。
- 正式开始新分支10301/10401各完整33development组成，PID358308/169936创建03:42:12.414840/03:42:12.977225，完整命令/输出保存；10501待两者结束。不重复原floor25控制，原6/99父与3/99失败保留，finaltest未用，未因等待/超时重启或hot换。
- 本轮限定原神器效果/实体/部队再发动/格挡消费/宝典同色材料与伤牌证据已存mode ledger_v2，未验原色付款/多目标/其他模式/后续消费者/UI保持pending。没有原规则code变化，没有扩大全库测试。


## 新10301冻结异常、原霸主联合预期与精确恢复
- 首核两358308/169936活进程与原创建时间/命令、输出更新，没有观察超时重启；后续都自然退出。10401完整结束，10301在79095899…101步保存后audit误报30步强化狂怒，不能当完整33/99结束。原fail trace/旧oracle保留overlord_rage_audit_failure。
- 原basic021卡图读红费1/强化攻4；原skill007.skills9魔力霸主给符合支付色的首张强化原输出+4并返中央标记，源码OnlyActualPaid/BoostExactAttack一致。修Rage联合oracle，要求实际非free/无限Red强化，精确首匹配标记返还、其他标记和旧物理攻击贡献保持。101全实际重放2339检查零错，无发布霸主实际30步新增4旧2保持；7变造负例拒绝，原规则/Mask没有变化。技能图片007实际并非hero pool007，未据它猜原文本，使用明确原JSON及旧账本源指针。
- 新resume_frozen_discovery.py通过完整10401参考bind model/pool/drafts SHA，对历史前缀必须逐步核state/fulllegal/saved概率/RNG采样/after/raw，精确恢复独立actor RNG再跑剩余任务；原失败报告仍保留，新恢复结果另有prior_report。Python编译通过，实际恢复PID244920创建05:03:46.653602/完整命令保存verified_recovery_launch。不重复已成功任务随机尝试，只做恢复所必需的同链验证，尚未完成不得声称全99。
- 新经验overlord_rage_exact_bonus_and_frozen_recovery加入AGENTS索引。Unity连接尚未恢复、无工具可取Console，Console两读与场景门禁继续pending，不伪造通过；目标仍active。


## 精确恢复完成、采样实验1/99与两个输入/模式问题
- 244920实际恢复已自然退出、stderr空：20历史任务1629真实步骤逐次实际所选概率/RNG选择/state/after/raw一致，剩余连续完成；10301完整1/33、63081有限检查0errors，原一个失败报告保留prior_report。源历史未保存完整概率向量/完整legal列表，不能冒称原整mask逐项相等；验证当前完整mask中的实际成员及保存所选概率，额外数学结果不回填未知字段。executed_recovery_source.py留存对应执行SHA，不猜RNG。
- 第二10401为0/33，65807有限检查零错。最后10501 PID114236创建05:14:17.285556完整命令保存，后自然退出、0/33、58902检查零错。三流完整1/99、187790有限检查零错误/冻结0更新，未胜过父6/99，未晋升；full_verified_frozen保存，finaltest未用。不继续把多采样23/96当改善。
- 1191实际free强化狂怒案例展示raw For choice与实际mode区别。新增effective_card_modes按原frozen exact choices、非spell NextEnhanced及真实物理移动辨析，不数规划/材料；实际案例+4分类边界通过，同96当前DLL/原数据绑定123不同有效模式、0unknown。旧事件/144跨历史模式代理未回写，不把该96子集当全当前lineage；Python编译通过。
- 只读当前public_entity_tokens证明卡entity只有身份/张数，原费用/效果文字未进入actor。新增printed_entity_text原公开8JSON字符/字符对128静态特征；一处原共享双面无total_description解析异常留说明后按side1/side2修，保持291词表不加非原项。不可变表、padNaN屏蔽、无关数字不改变文字、歧义ID拒绝、双面特征等输入门禁通过。尚未接actor/完整梯度/旧矩阵Adam/RNG迁移或真实PPO，不能宣称训练新模型/语义理解或改进。准备后续单变量公开信息输入对照，而非进一步盲调重复小课程。
- 新经验effective_free_modes_and_public_printed_input_gate加入AGENTS。所有代码仅Python，原C#规则未变；Unity工具/编辑器连接仍缺，Console两读/自主实际操作不得冒充通过。goal active。
