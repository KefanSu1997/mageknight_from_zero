# PPO真实轨迹发现笨重零攻格挡观察遗漏

## 新失败与修复

v38新PPO进程121232实际运行后自动终止，stderr明确 Independent transition audit failed；没有因观察超时重启。失败保留在 `AutomationOutputs/BattleRl/20261004_printed_original_v38_fresh_ppo/training_rule_failure.json`：seed38489，Norowas，原048（三个1点物理攻击、寒冰抗性、笨重），step26真实移动4→3、第二次攻击1→0，规则要求成功格挡但trace blocked=false。旧step27随后又选了零攻的格挡操作。

确认共用规则 `NatureRevengeActions.RecordZeroBlock` 已把确切攻击记入 `ActionContext.SuccessfullyBlockedAttacks`，但 `PveBattleSession.IsBlocked` 仅查私有手动格挡集合。修复正式查询读取两者，让UI与PPO和操作许可使用相同真正格挡状态；没有放宽“零攻必须视为成功格挡”预期，原伤害/费用公式不变。

新增原048三真实攻击槽、两实际原000基础移动消费者测试：先支付第二攻击，其他槽不受影响；已处理攻击重复付款拒绝；再支付其余两槽，正式显示全格挡、移动余1、手伤0。

## 受影响检查与旧错误测试

构建成功后首次运行新检查及PveSession/承伤悔棋回归 `20261004_054106_e12a4719` 为6/7。失败是已存在旧“攻击不足不削甲/城防拒绝远程”测试：原017实际忽略地点城防且高甲8，测试却把它当城防/护甲4并预期不足攻击仍被消费。原失败保留，修正测试选择真正原025城防/护甲7，强化检验两次不足攻击均正式拒绝、实际攻击贡献2/4保留且护甲始终7，不修改规则实现来迁就旧测试。

定向补测 `AutomationOutputs/RulesState/20261004_054353_39b48aa1` 2/2通过（新三槽笨重及旧失败），已通过其他6项不重跑。

新冻结 `tmp/battle_rl_v39_zero_block_observation/BattleRl.dll` 构建0警告0错误后，以实际保存seed/动作前缀重放：`AutomationOutputs/BattleRl/20261004_v39_native_zero_block_failure_replay.json`。前27步均正式合法执行、651项审计零异常；原第二攻击零值且blocked=true、原第三格挡保留、第一未挡；旧step27重复格挡不再合法，发送拒绝后完整实际状态不变。原完整失败不覆盖，不把修复前缀等同于重新训练通过。

Unity Console两次zero gate：`tmp/rl_v39_zero_block_console_20261004.json`。没有完成动态召唤UI/本项实际场景操作验收。

## 导入进程资源操作纠正

检查真实Unity窗口发现本版本日志 `Failed to set desired worker count 0`，执行工具的“success”并不能证明Native API接受0。先完整保存 `tmp/rl_import_worker_zero_rejection_console_20261004.json`，再设合法1并通过官方Force API等待空闲导入工作进程管理：`tmp/rl_import_worker_single_20261004.json` desired=1/updating=false。旧0失败保留，清理已保存的历史错误后刷新，Console两次无新错误。会话原值8仍保存在SessionState，可在结束训练资源任务后恢复。实际Unity可维护硬件导入子进程，不声明进程总数为0或1。

恢复前实际内存余量物理约1.48GiB、提交约9GiB；新模型启动需继续监测，未终止用户其他程序、未重启电脑或修改VPN/包/虚拟内存。

## 恢复训练与范围

v38已确认终止且无检查点文件。v39从新策略继续训练工程目标，独立目录 `AutomationOutputs/BattleRl/20261004_native_zero_block_v39_fresh_ppo`，8192目标步、seed38489起、欠覆盖窗口28、原开局、条件动作PPO、lr0.0002、每60秒新增实际轨迹审计；启动前确认无活训练或v39协议进程，PID/启动时间以launch为准，不重复启动。

仍未取得新模型完成、全原要素实际覆盖或泛化与Unity最终验收证据。旧全范围缺口继续保留，不能把抓住并修一项bug当作模型目标完成。


## v39 第一批真实训练轨迹定向审查

2026-10-04核实同一PID195808、启动05:49:35仍活跃，没有重复启动。首批1024步/27训练episode，耗时258.87秒，约3.96步/秒；不能外推完整训练效果。日志覆盖计数49敌人、7英雄，是训练统计口径，非49敌人全部规则已通过。尚未取得新检查点或完整胜率。

新增七英雄实际完成episode审计4474项零规则异常、7项formal_end_turn_denied缺口。逐读取这七条末尾前后真实状态：全部activity=0，手牌只含11–21张真实伤牌，不能作为合法回合末弃牌；拒绝后zones/tokens/crystals/source/held/units/turns/rounds/completedTurns/completedBattles均不变。部分真实轨迹已进入第二轮，但本批不声明完成跨轮旅程。策略在全伤牌时继续发起战斗，导致回合末活动门禁拒绝，保留质量/入口缺口，不修改规则门禁来给训练放行。

定向原文审查：原Goldyx叉形闪电每回合最多三不同目标分别远程冰火1，本次Targets=5实际绑定敌0、2，两贡献各1、每回合技能锁写入、魔力未变；本轨迹未见后续攻击消费，不能记为完整技能消费者通过。原Norowas领导+乌顿弩手第2实体：印刷远程2+领导1实际远程物理3，同实体耗竭、技能次数消费，无印刷魔力费故库存不变，step9实际攻击消费该贡献并击败真实敌人。审查及源SHA见 `AutomationOutputs/BattleRl/20261004_v39_1024_manual_trajectory_review.json`。

Unity execute_code实际观察：当前Part1_PveBattle、非Play、worldEnemyCatalog仅7（032/007/066/002/028/025/027），已有7图无缺失。原棕色供给和其他62敌人的场景绑定尚缺。DrawStage/SkillTarget/CardTarget/EnemyDefend/WorldSpells仍通过Encounter.enemies.First查原出战敌人，动态召唤实际Brown不在原出战绑定会失败。此轮仅定位、未修改场景或UI，不能声明动态召唤UI通过；下一步须复用全部原素材完善绑定与目标查询，再编译两次Console零错误并实际场景操作验收。

续核实同一活进程完成2048步/52episode/506.77秒约4.04步每秒，新增真实审计5245项零异常。训练统计已见62敌人、45技能、28部队，不等同全部模式及消费者通过。新增审计6缺口中4项伤牌活动门禁、2项策略从未尝试战斗（seed39819/39765），七条最近episode均未完整赢得一战；不称可用测试模型，不拿规则审计零异常替代胜率。尚无npz检查点。后续须观察策略更新后实际战斗成功和完整旅程，以及补齐UI原敌目录。
