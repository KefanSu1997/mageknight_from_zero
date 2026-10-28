# 随机应变的非伤牌材料词表必须包含真实神器

v91 seed440897原67步失败：效能过载基础复制随机应变，先以实际基本000、006作材料，最后剩启迪头环022而待执行1次，旧RL legal空。原印刷023只要求另一张非伤牌，正式ImprovisationEffect接受任意实际非伤手牌；起手材料预算已正确包含神器。错误在RL InputProfiles材料只列DeedIds（行动/法术），不是正式材料不足。失败前缀、seed与SHA保留 `AutomationOutputs/BattleRl/20261006_v91_unclassified_deadend_failure.json`，不能归为已知只伤牌死路。

唯一规则桥接改变：023材料列表加既有25原神器ID，复制宿主024与repeat通过既有递归输入自然获得同入口；永久去除等其他牌的材料范围不扩张，不设计新卡，不削弱材料预算。新增800完整动作。显式schema v92防错误旧权重；5704观察坐标不变。迁移严格比较所有旧完整Key/定义、原类型名/能力布局和原25神器清单；恰好800新增定义、新头零初始化，旧输入/值/类型/动作头逐列精确复制。旧合法列投影不变不等于完整新分布不变。

验证：原失败新旧DLL67操作配对，规则状态/观察和旧合法动作投影完全相同。最后区别仅旧缺入口导致提前done/no_legal与-2处罚，新有合法续行；真正discard物理启迪头环（非焚毁/非发动），完成原最后move3并可正式nextTurn。直接基础3、直接强化5、过载强化两次10均走同真实失败前缀分支，原红费/实体/神器未发动检查通过。强宿主必须选供体strong_move，先误选base move的诊断失败另存，不冒充新规则失败。

迁移四负例（遗漏新定义、修改新字段、错观察坐标、错旧类型头）及旧定义修改负例检出。构建0警告0错误，Console两读0错误 `tmp/rl_v92_artifact_material_console_20261006.json`。实际资料：v92_improvisation_material_migrated/migration.json、v92_improvisation_artifact_branch_checks.json、v92_migration_negative_checks.json（均AutomationOutputs/BattleRl）。未改Unity场景，不宣称最终保存模型场景验收。

先核实本任务PID295228/07:49:33完整命令，因确认适配器遗漏停止本任务训练，不是观察超时重启。只保留实际24576检查点；27648等未保存更新不能冒充恢复，Adam也未保存。新DLL使用历史按门禁不继承，旧实际证据分开保留。完整目标和原费用/所有消费者/70自身技能/新种子泛化/Unity最终验收仍有缺口。
