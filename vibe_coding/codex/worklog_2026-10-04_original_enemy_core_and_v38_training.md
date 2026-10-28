# 原69敌人核心字段与v38 PPO恢复

## 数据来源与修正

依据已逐图查看的69张原敌标记及独立棕敌预期，整理 `AutomationConfigs/original_enemy_combat_oracle_20261004.json`，逐ID保留原图SHA256及印刷攻击槽、基本/低高护甲、抗性、能力、名望、防护和召唤数量。另复核013/018/019/043/048/057/060/061及012/056/022/047/055/058的复杂图标，确认吸血、城防无效、双甲、多攻击槽、防护与元素。

发现43个原敌定义存在字段差异（25个能力、6个攻击、24个护甲对象、2个防护字段；计数可重叠）。原012火焰攻击及防护1、原013吸血、原043低护甲6/高8并吸血、原048三个1点攻击带笨重、原018/020/061忽略地点城防等均恢复印刷定义。原019的红骷髅是残暴，额外名望图标保留独立规则范围，不误当毒素。

先保存 `AutomationOutputs/BattleRl/20261004_remaining_original_enemy_metadata_failures.json` 原始字段与独立预期，随后修改正式 `resources/text_json/monster.json`，同步 `Assets/GameData/BattleDemo/original_monsters.json`。原Brown失败、v35召唤失败、v36容量失败均继续保留，不覆盖旧失败。

新增 `OriginalEnemyPrintedMetadataTests` 69个原图/实际解析器对照及1个Unity打包源逐字节一致性检查，防止命令行已修而Unity仍使用旧数据。

## 后台和编译验证

新RulesCli构建明确成功、0警告0错误后才执行新增与受影响范围。`AutomationOutputs/RulesState/20261004_052358_5c462ce1`：118/118，涵盖70项原敌核心/打包源检查，以及棕敌、召唤和Tezla双甲/防护消费者回归。

Unity刷新后 `tmp/rl_v38_original_enemy_metadata_console_20261004.json` 两次 error/count1000/includeStacktrace 均零错误。核心字段通过不能替代全部原敌实际交战、阵营奖励、物理副本分布、名称/类型或Unity操作/显示验收。

## 内存与训练恢复

启动前可用物理内存约0.85GiB，系统提交余量一度不足2GiB。核实额外Unity进程的父进程属于本项目153276，均为Unity内部AssetImportWorker，非用户第二个项目。通过官方 `AssetDatabase.DesiredWorkerCount=0` / `ForceToDesiredWorkerCount()` 管理空闲导入工作进程，原值8存于 `SessionState`，仅当前会话，不修改包、不手工杀进程、不关闭用户程序。API结果见 `tmp/rl_import_worker_release_20261004.json`。Unity后续可自行重新产生导入工作进程；以实际内存重新测量判断，而不把API成功当作持续零工作进程。

启动前重新测量物理余量约2.88GiB、提交余量约8.26GiB，才启动新训练。新冻结DLL `tmp/battle_rl_v38_printed_original_enemies/BattleRl.dll` 成功构建0警告0错误。v38更新数据域范围，继续使用v37六目标/父实体特征布局，旧v34/v36模型不兼容，不错误复用旧输入前缀。

新PPO目录 `AutomationOutputs/BattleRl/20261004_printed_original_v38_fresh_ppo`：8192目标步、独立7案例小型基线/评价、每60秒新增真实轨迹审计、从seed38375起欠覆盖窗口28、学习率0.0002、原开局与合法中盘构筑、模式→合法原元组条件策略、全新权重。manifest补充原敌JSON、独立印刷预期和Unity打包数据SHA256。

PID121232，2026-10-04 05:34:38 +08:00启动；已通过命令行/创建时间核实唯一实际新训练进程，environment及manifest已写出，stderr为空。此时仍在基线准备，尚无新训练步数或新效果/胜率结论，不因stdout暂时无输出重启。完整启动参数和之后输出以该目录launch/run_manifest/stdout为准。

## 后续与未完成范围

持续观察同一实际进程；新轨迹才新增审查，规则异常保留seed/动作后停止，不放宽预期。重点检查新召唤、原吸血/笨重与原防护/双甲。全121原牌、7英雄/69敌人、70自身技能、30部队等真实费用与消费者训练覆盖、场地上下文、未见完整构筑评估及保存模型Unity回放仍未完成。还发现UI从遭遇预设查原图时无法直接找到动态召唤棕敌，需修正式动态原实体显示入口并实际场景验收，不能用本批编译零错误冒充通过。
