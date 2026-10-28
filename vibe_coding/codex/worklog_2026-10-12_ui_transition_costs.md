# A提交→B提交的条件交互成本

用户指出需计算A结束到执行B的衔接。本轮只读取既有轨迹/实际exchange，不改Unity规则、布局、奖励或训练进程，不新开实景测量。

历史同局相邻关系850,374条/1,009种，严格等于867,821步−17,447非空局，不跨局连接。条件P(B|A)只除以有后继的A数，不能直接除所有A。source→play19,683（有后继source的26.42%）；play→source17,081；play→play36,908；sideways→sideways10,425；unitDamage→unitDamage57,744（73.83%），后者不能直接断言UI循环或剥夺必要规划。

统一会计边界：A最后一次成功native提交→B下一次成功native提交。中间非提交点击单列（可能选牌、导航、查看信息或准备后续C，不硬推断全部为B准备）；B提交1次。距离自A提交中心经过所有中间点击直到B提交中心。reset、拒绝、未知native变化和session隔断，保留barriers。未支持映射保持未知，不猜全量成本。

从2个公共魔力完整脚本、3组baseline及7个历史UI策略episode的实际exchanges，提取67个受支持间隔，按脚本/策略/版本/面板/阶段分组，不作总体平均或人类频率：

- source→play2实例：A为UseSource实际取到token，CloseTools1个中间点击，Play1提交，1106.486px/1面板边界；原牌/强化此前已选好。先前canonical source5点击包含CloseTools，本次semantic source4点击不含Close；不能将两种会计方案相加。若A以“取骰且返回”结束，首段到Play约665.1px，不再计返回。
- sideways→sideways15脚本实例：下一张选择1中间点击，再确认1提交，3274.489px/0面板边界。原单动作1637.2只含选牌到确认；本次补回A确认位置到B首控件的移动。固定左侧首张实例不外推所有卡位。
- sideways→attack9脚本实例：目标选择1或另加分配/All/Done共4中间点击，Resolve1提交，1713.015—3167.050px/0—2面板边界，详细路径可选不叫自动冗余。

验证：转移总和/每个条件概率行和、67个间隔click与distance分区恒等、同session、真实play正例及注入拒绝/reset负例均通过；py_compile通过。没有调用elapsedSeconds冒充交互延迟。两次分析结果分别保存，第二次增加HTML与明确的中间点击作用限制，不覆盖第一批。

输出 `AutomationOutputs/UiFluency/20261012_transition_costs_verified/report.html`，results.json、transition_distribution.csv、source_files.jsonl（逐exchange SHA）及verification.json。优先级应从单按钮频率扩展为条件转移的可避免成本；当前例子不能乘全部19,683次source→play冒称历史实际开销。
