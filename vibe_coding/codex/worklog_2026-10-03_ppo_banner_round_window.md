# 原附旗轮末保留/取回决策

前轮为progress：补欠覆盖采样及计划成本、真实轨迹精确重放。本轮核实v19进程209568持续运行，并审新实际轨迹；不启动第二训练器。

## 当前实际训练

v19到8192步/61episode，7英雄37敌28部队、157模式74输入43技能选项66部队能力178消费者。固定七英雄小样本6/7至少清一组、9完整胜场，平均155.14步；不是通用泛化结论。保留7轨迹933操作、2完整胜场/27启动战斗、无超时、连续计划最多91；不同种子子集不能当受控因果证明。该保留子集未出现新的宝典/旗使用，不能把目录或教师定向验证当实际PPO覆盖。

后到11264步/87episode，7英雄39敌29单位、182模式98输入48技能选项72部队能力210消费者；最新15614现有断言零错误。v19仍使用启动时冻结Python/C#，不声称新增v21在当前训练中已运行。

## 修改

`JourneyTraining.cs` v21将原单玩家正式轮末的附旗窗口暴露为PPO动作：正式EndTurn/升级/AnnounceRoundEnd后，只有Game.CanChooseRoundBannerReturns对应的实际附旗存在时停在窗口；roundReturn为实际自有单位选择保留/取回；roundFinish调用正式EndRound，再PrepareRound/原night_1/BeginTurn。无需重建玩家、部队、卡牌或守军，轮末先清理再新轮洗牌。窗口禁用旧战斗、出牌与其他回合操作，物理归属由正式ChooseBannerRoundReturn检查。

追加真实窗口和各单位取回选择观察，原观察前缀及动作Key保留。roundReturn属于连续计划成本范围；原合法反悔仍保留。抽取BeginFollowingTurn/FinishRound共用逻辑时，遇敌偏移必须按原已递增Turns计算；本次在递增前缓存用Encounter(Turns)，不引入少前进一场的变化。

`trajectory_audit.py`区分轮末窗口保留的最终战斗历史与实际下一场，检查轮末选择不提前移动卡/推进时间、正式结束轮次不多算回合、新夜轮开始恰好一次。train_ppo仅在实际roundFinish后记录真实保留/取回消费者，不把预选作完成；行为报告包括轮末反悔。新Python只在下一批加载。

## 验证

冻结 `tmp/battle_rl_v21_banner_round_window/BattleRl.dll` 编译零警告零错误；`verify_banner_round_window.py` 13定向/269转换检查：实际seed19原勇气旗分别保留、取回；相同物理实体在正式EndRound之后回洗，夜轮真实抽到并再次附着。证据 `20261003_banner_round_window.json`，仅后台生命周期，不是Unity验收。

受影响真实seed3376的132操作在v21精确重放全部原字段与合法动作不变，72计划奖励按声明成本改变。`20261003_planning_cost_exact_replay.json`当前scope为v21；先前v20控制台输出与工作记录独立保留，不冒充旧冻结证据。Console两次零错误 `tmp/rl_banner_round_window_console_20261003.json`，后续Python计数/报告修改再读两次。

## 接续

等v19实际正常exit/保存终权重后，下一批优先使用已通过后台的v21冻结DLL，1024步烟测、coverage-window21、audit-seconds60，warm-start原最终权重和metrics.next_training_seed；无需另跑中间v20训练批（v21已经包含成本/采样并经定向回归）。失败时保留真实检查点，最小范围修复后再顺序续训。当前没有重启/并行任务。

指挥旌旗实际招募单位后取回导致移除单位的特别消费者本批未验证；头环/宝盒、全技能/复杂输入、实际地图场地、全部原要素训练覆盖、未见构筑与最终Unity策略回放仍待完成。完整目标active。
