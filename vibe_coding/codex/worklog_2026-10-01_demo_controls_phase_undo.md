# Demo 按钮精简与阶段悔棋

用户要求：减少操作按钮，同时保留每个战斗阶段重置，方便试玩悔棋。2026-10-01 完成实际场景验证。

## 实现思路与功能

- 战场右上常驻“重置本阶段”和“菜单”。出战配置、魔力源/战术、本阶段规则收进菜单；空部队入口隐藏。
- 没有选择目标时不显示结算按钮；选中目标后显示结算，多于一个实际攻击来源时才显示来源分配。保留真实原卡出牌、横置与目标驱动操作。
- 四个阶段首次到达时保存实际运行对象图。重置在原对象上恢复玩家、原牌引用、资源、敌人、伤牌、部队、技能次数及随机状态，不重建玩家、不重放开局。
- 菜单可回到本场已到过的远程/攻城、格挡、承伤分配、近战起点；退回后丢弃后续检查点，重新进入时按新的实际状态采集。战果页可“重打近战”。正式结束回合后不能撤销旧战斗。
- UI 输入和选中状态在恢复后清理，已存在的技能攻击来源标签恢复；此功能是本地 Demo 悔棋，不是持久存档格式。
- 对不支持的外部状态类型明确拒绝建立检查点并在菜单显示原因，避免部分恢复。

主要代码：Assets/Logic/Runtime/Battles/DemoStateCheckpoint.cs；Assets/Scripts/Battle/PveBattleController.DemoControls.cs；PveBattleController.cs。

历史验收入口同步：116 个 build_pve_*acceptance.py 的出战配置前增加菜单操作，索引 tmp/demo_menu_fixture_migration.json；补迁移七英雄构筑回归入口，以及 Utem 阶段规则入口。只迁移入口与进行语法检查，没有宣称重跑全部历史场景。

## 验证证据

- 后台：DemoPhaseResetStateTests，5 项测试、52 条断言通过，AutomationOutputs/RulesState/20260930_191203_cb69be66/results.xml。覆盖同对象引用、真实战斗重放不重复发奖、部队状态、技能次数、集合扩容、随机状态及拒绝不支持类型。
- 场景：tools/build_pve_demo_controls_acceptance.py，60 操作、121 断言全部通过。报告 AutomationOutputs/PveBattle/20261001_022637_10c53dd2/report.json。
- 实际操作覆盖：原狂怒强化支付红晶并击杀后恢复原牌/红晶/名望/敌人；恢复后按真实来源完成两个目标的攻击；战果撤销两敌奖励；回到承伤前撤销实际伤牌；回退格挡和远程；横置格挡消费后恢复原牌/格挡池；承伤阶段重复重置。
- 保留六张关键转折图，本地查看了近战恢复与远程恢复画面；未上传截图或运行轨迹。
- Unity Console：编译后及场景后各按 count=1000、error、includeStacktrace=true 读取两次，均零错误。证据 tmp/demo_controls_console_pre_20261001.json、tmp/demo_controls_console_post_20261001.json。
- 额外手动操作：原托瓦克16牌战斗试玩 → 原捷足先登 → 开始回合 → 兽人战团 → 打开菜单 → 返回战斗。菜单布局已目检，未遮挡内部选项；未到达阶段的重置禁用。Demo 保持在真实五张手牌/十一张牌库的战场，可继续试玩。

启动时 Unity Hub 原窗口持续空白，重启 Hub 后通过项目列表正常打开 6000.6.0f1；未通过命令行直接调用 Unity、未改 packages、未改系统/VPN、未推送远端。
