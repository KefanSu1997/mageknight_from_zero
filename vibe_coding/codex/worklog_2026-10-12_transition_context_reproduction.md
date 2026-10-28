# 将“尚未测量”落实到真实 UI 上下文复现

用户质疑“未知总体成本；见条件实例”：UI可以复现。该标注不应暗示无法测量；此前仅有部分固定上下文样本，缺的是批量复现。

新增 `measure_transition_contexts.py`，通过原UI开局、点击、真实阶段reset，不改规则/布局/训练。两个新开局均为Ranged原敏捷：第一源面Gold/Green/White，取白骰；第二Gold/Red/Red，取白天金骰按原白费支付。每个开局先选牌强化再取骰，以及重置后先取骰再选牌强化，两路径各8点击，共4个完整实例32点击。reset后实际sharedRuleState及source.faces完全相等；两路径执行后native公开规则投影相等。比较不称完整隐藏状态/Unity RNG认证。

A=实际UseSource提交、B=Play提交：已选牌/强化，上下文间CloseTools1点击+B提交1，1106.486px/1界面；尚未选牌，CloseTools/选牌/strong3中间点击+B提交1，2085.652/2821.302px/1界面。整条任务仍8点击，只是操作顺序和手牌位置影响A→B的分区，不称未选牌整体多2点击。

两个开局没有两张当前可用的普通攻击卡，因此附加play→play分支保存base_attack_gap，没有冒称该新分支已测。旧间隔样本仍单列。新增32点击1728共享字段对照0差，Console双扫描当前error0，两个Python脚本py_compile通过。

新报告 `AutomationOutputs/UiFluency/20261012_transition_costs_reproduced/report.html` 将矩阵每个已测pair显示实例数和中间点击范围；其余标“尚未UI复现”，保留旧报告与旧结果。合并旧/新实际间隔75个，仍不能把混合当前/历史UI实例成本当全历史平均。fresh_context_costs.json提供每个配对条件的数值、操作列表和源SHA，verification.json绑定实际证据。
