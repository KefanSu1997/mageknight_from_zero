# 每组20条与持续任务

用户明确“每种转移组合最多20条，持续推进”，并回复选择1009行为名组合、组内涵盖参数和阶段。已建立active持续goal，不标complete，后续自动继续。

先生成两种固定均匀哈希优先级计划：1009 Kind对12350样本；91158参数阶段对269146样本。用户选前者后生成 `20261012_sample20_selected_indexed/selected.sqlite`，1009组/12350条、最多20。<=20参数变体的组逐变体分层；更大组采用阶段+参数哈希桶分层，每个非空层至少一条，剩余额度按人口分配。层内均匀固定优先级无放回，estimator_weight=层人数/组人数/该层样本数，每组权重和1，整体再按历史组频率加权。采样失败不替换，稀有组不补20。第一版无索引扫描中断保留，索引临时表版本通过；原均匀计划不覆盖删除。

新增 `PveBattleController.CostFixture.cs`，仅在显式StructuredUiPilot SessionKey启用时允许 `reset_task`。使用原英雄/原构筑、各独立seed、原世界SHA、真实PveJourneySession、部队/技能/晶体合法检查；构造完全成功才安装Controller。没有按观察伪造post-state，也没有改训练/奖励/Assets Logic核心。仅支持明确Build或OriginalOpening的declared任务，legacy_seed仍需适配。通过原敌群数据生成内存Encounter，用后释放自有生成对象，不删除原资产。

StructuredUiPilot新增taskJson/reset_task，及仅cost fixture存在时附加 `audit.costFixture`只读draw/skills/units/sourceOrder/unitOffer/movement/influence；这是测量校验，不进入既有actor白名单。

第一次编译未导入新脚本，Host报缺ResetCostFixture；force all refresh后消失。增加只读审计时写错MovePool属性，记录编译错误并停止该自有测试进程，修为MovementPool，编译0后重新启动新证据目录。首次源骰比较错误用了sorted source而非auditSourceOrder，只这一项失败留存；改为原物理顺序字段。

七位英雄原真实旧task根，通过实际Unity重置：公开23字段/根共161，进一步加入按auditDrawOrder恢复的完整牌库顺序、技能、单位、物理源骰、供应、移动影响和phase，共217检查0差。当前Console双0，旧错误/失败仍保留。证明declared根初始化入口，尚未证明任意前缀恢复或任何完整成本样本。

输出可查看 `AutomationOutputs/UiFluency/full_cost_progress/report.html`；当前completed_samples=0/12350，217检查是初始化门禁不能填成成本样本。没有假称全部数值已完成。目标保持active。

## 下一轮直接继续实现

1. 实现native prefix准备，严格复用tools/BattleRl/JourneyTraining.cs及其partial的正式API调用；维护pending payment/round等必要状态，不从公开快照手工猜历史。actual测量只计A/B展开的pointer，prefix/reset单独归档。新NativeSetup若复制上游实现需源SHA/当前规则绑定，勿构建另一套游戏规则。
2. 带参数动作到动态实际controls的自适应映射，先实现advance/source/sideways/play/unit/skill，再覆盖承伤顺序、攻击/格挡mask与材料/共享技能等。使用当前可点击位置与raycast，逐步核规则效果；缺映射不能记0或∞冒充完成。
3. sample worker逐条写冻结selected.sqlite样本的attempt/measurement，保存click路径和位置/面板/前后state；不换样本。按已确认Kind组做完整矩阵，source参数仅作为样本层。
4. legacy种子重置及旧规则不兼容要验证；真实OriginalWorld场景的fight动作与UI实际地图入口可能不同，必须核对，不能加测试专用按钮后称原UI成本，也不能把无界面原生API动作算点击。
5. 全部组和选定样本覆盖后再允许整体成本导出；改UI后以同样采样和权重重测。维持持续goal至完成，除用户明确停或满足blocked三轮门槛外不自行暂停。
