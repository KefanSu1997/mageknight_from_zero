# 持续goal：原前缀恢复与首批冻结样本成本

本goal turn有效进展：生成615行CostPrefixAdapter，连接prepare_prefix RPC，真实3原episode根和前12步后6个完整原生快照严格逐字段相等；实际回放两组各20冻结样本，最新24/40通过，录入selected.sqlite与进度表，未标goal完成。

`generate_cost_prefix_adapter.py` 提取当前JourneyTraining及World/SharedSkills/Shapeshift/NativePayments/CombatConversions的正式API调用、ResolveInput与RawState，以源SHA/生成SHA绑定。用于原生setup，非动作成本、非另写游戏规则/训练核心。Unity兼容修复：表达式方法提取须包含Select尾部；Move→CostReplayMove只替类型，不能把Journey.Move方法改名；Move使用普通类与WithCardInput代替新record，保持字段；Monster显式原规则类型别名；internal审计字段用只读反射Getter，不加public setter。三批编译失败保留，当前编译与实际Console双0。

Controller通过ResetCostFixture创建adapter；prepare_prefix只运行原API且逐步读取audit，完成后同步实际Journey.Battle、敌群cursor并清UI选择。pending payment尚无UI绑定会明确拒绝，状态留存而不是造输入。Setup/prefix单独归档，不算点击成本。新的audit.costNativeState完整原规则状态仅用于校验，不供既有actor白名单。

原规则Legal预览会给假设创建的临时卡分配审计ID，正式pointer不做那些预览，后期轨迹出现ID间隙。因此数字label不能直接作跨进程实体本体；cost_entity_identity.align从所有实际zones、原手序/实际抽序与敌人存放区域建立双射，重标handOrder/auditDrawOrder、cardInstance、requiredEnemy、材料/傀儡/防护等引用。保留实体内容、顺序与引用，不能仅删instance字段。原ID错误失败批保留。正例重标+1000通过；token变更、手序交换、错误实体内容负例检出。游戏效果比对只排除训练协议activity/planningRun；stopReason仅在no_legal_continuation/step_limit时排除，因为这些来自协议mask/预算而非UI动作，原formal失败原因不忽略。

measure_sampled_ui_pairs.py第一版实际适配advance、plain sideways及手牌7张循环翻页（读当前真实页码与实际raycast可点controls），声明中性选择/原前缀后第1页为基准UI pose，不声称恢复历史未存的UI选牌/鼠标。每个实际click用既有measurement timing，原snapshot-before及after效果逐字段核对（实体审计label双射之后）。没有直接调用native API冒充A/B点击。

冻结原sample组 advance→advance / sideways→advance 各20，最新 `20261012_sampled_paging_verified/results.json`：24完整/16失败；16都是尚待legacy seed初始化，未换案例。13个advance样本2点击、A确认到B确认0px；11个sideways样本通常3点击、A横置确认到Badvance约339.8px，含翻页的实例单独记录。完整source pointer/mask无扩泛化claim。之前first_worker/identity_aligned/protocol_aligned批与错误保留。

ingest_ui_cost_results保留attempt SHA、源path、选定case与成功raycast，costs只收validated complete，failed状态needs_adapter不补0。当前进度24/12350、0/1009全组完成，表中数值明确是已过样本均值，不能代替整组加权成本或整体优化。write_ui_cost_progress更新同一浏览器报告。

## 下一步

1. legacy_seed成本fixture初始化：按原scope/Flag/seed复用原Env初始化算法，核根/前缀。不要用容易通过的declared样本替换16个失败。这能使已适配两组完成20/20。
2. 增加source/play/unit/skill及所有复杂输入mapper，再扩展attack/block/ordered unitDamage与shared/world等。新handler从动态UI观察绑定，不猜slot/坐标/材料身份。
3. 集成sampled checker/加权成本汇总，只有所有选定样本足够的规则效果证明和实际成本后才交付总体。当前report粗均值仅阶段性；未执行全量12350。Goal保持active；不是blocked状态。


### Task-header recovery and public mana pointer routes

The earlier label legacy_seed was provisional, not evidence of seed-only generators. Exact task IDs in certified/unknown pool sections and declared episode headers recover 4,175 omitted independent definitions; nine witness files preserve task_definition directly. All 4,184 recovery records retain original seeds and registry SHA/source provenance. Six early integration headers remain unresolved. No samples were replaced.

The same initial forty selected pairs now have 34 accepted measurements, with another 10 strict native effect snapshots passing in the pending-only recovery batch. Public mana mapping uses actual physical source order, battle menu, tools panel, die and commit raycasts. The return from source tools belongs to the next action approach. Partial failed pointer sequences are retained. A sampled artifact sideways intent exposes a real UI gap: DrawArtifactActions bypasses the normal Btn_Sideways branch; observed artifact controls contain no sideways button. This is not assigned zero cost.

Results ingestion now requires SHA binding to the exact independently verified result snapshot, preventing a live file advancing between verification and ingestion. Report means use stratum estimator weights and disclose accepted weight coverage; incomplete means remain conditional.
