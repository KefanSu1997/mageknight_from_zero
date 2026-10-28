# 显式合法构筑与纸面覆盖不能代替实际发动和可解证明

新增Task.Build（协议v2）：Family、Fame、AdditionalCards、UnitIds、SkillIds、Crystals、InitialWounds。起始原16牌保持，额外牌只允许既有高级行动/法术/神器且原ID唯一；技能须实际英雄归属、数量符合原等级；单位保留原定义且普通指挥槽校验，不借忠诚保留槽给额外普通部队。魔晶仅命名基本色0至3，数字Enum别名/金晶拒绝；伤牌经原PlayerState.AddWoundCards(toDiscard)及正式洗牌/回合初始化，没有猜测伤牌规则。魔宠经正式轮初维持消耗真实魔晶并获得调谐。声明中期拥有状态不冒充自然取得历史。

原随机legacy/独立v1未提供Build时保持原路径。新DLL另存 `tmp/battle_rl_explicit_build_tasks`，不热换活进程。显式构筑4项真实检查、原独立因素/legacy原坐标/mask/reward12步6项检查通过；无原规则修改、无场景修改，构建0警告0错误/Console两读0错误。

`coverage_tasks.py` 生成272未知候选，基于组成（忽略RNG/卡顺序/敌群排列）固定train/validation/test分组，相关随机变体不能跨split。八构筑家族必须实际带对应原支持卡，不能只有名称标签。原英雄起始牌、全部额外原卡、本人技能与原部队形成纸面模式/ID意图；hero-enemy483格作为组合关系。优先选择补缺权重/成本大的任务，149训练候选覆盖纸面请求除4项外；不得把预期出现/初始拥有当训练实际使用或模式通过。

最初有20个训练hero-enemy计划格只落在保留split；用不同的合法组成生成训练锚点，不将原验证/测试配置移进训练，最终训练pair计划无缺。基础011/025在当前七英雄StartingCards均无归属，额外Basic不任意塞给英雄：4个base/strong计划缺口保留，需原来源研究或正式合法取得路径，不删除需求。

272候选逐个实际inspectTask：原追加牌唯一存在、单位/本人技能及伤牌实际对应，全部初始化通过，0可解/0胜利池准入。可解搜索/不可解证明尚缺，未知隔离；纸面覆盖测试3项通过但不是数学上全部高阶组合或全部费用/材料/消费者覆盖证明。法术强力需正式Night，计划较长序列仅提供可能，不授予黑色魔晶或保证实际跨昼夜。

产物 `AutomationOutputs/BattleRl/20261007_coverage_task_drafts/tasks.json` 与 `initialization_review.json`。未下载大依赖、上传轨迹或推远端，旧证据/旧冻结模型保留，不继承不兼容规则计数。
