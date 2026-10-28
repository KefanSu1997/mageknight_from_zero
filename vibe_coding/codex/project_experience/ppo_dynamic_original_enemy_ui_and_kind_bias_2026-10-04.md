# 动态原敌 UI 绑定与分层战斗偏置

## 场景不能只查出战原名单

真实 Unity 场景只绑定7原敌时，后台69原敌和棕色召唤供给并不能证明场景可用。正式召唤在防御阶段替换实际敌人，战场/目标/防护等界面必须复用完整原素材目录；目标继续用实际实体引用和下标，不能按同名合并。独立战斗也必须传正式 EnemySupply，不能只有连续 Journey 带供给。

编辑器同步完整69原素材，构建入口同样维护；目录缺图明确拒绝。验收 art.enemies 检查当前实际敌人，而非原出战名单。自动操作原023双召唤（seed154，既有原构筑）实际抽到021和011，第二目标可选择、承伤后恢复023、两实际标记归还。27项受影响断言通过，原图/数值/阶段和无截断截图确认，Console重载完成后及场景后各两次零错误。

证据：`AutomationOutputs/BattleRl/20261004_unity_original_double_summon_labels/report.json`，关键截图同目录 captures；`tmp/rl_dynamic_summon_labels_final_console_20261004.json`。前一版显示空攻击和英文MagicResist的截图保留在 `20261004_unity_original_double_summon/`，随后明确显示召唤棕敌×2和魔法抗性。

仍未证明原盒标记拷贝数量、全部动态目标分支和保存模型完整UI回放；本次只是正式独立战斗入口和原双召唤流程证据。

## 分层策略的偏置必须施加到类型门

v39实际4096步固定七英雄评估0/7，无战斗完成。以保存检查点与冻结DLL实际执行seed100000首16步，策略先rest，正式休息锁使本回合不能战斗，其后播放原牌和使用部队不能冒充战斗消费者。保存权重SHA、真实动作前后状态和377项审计见 `AutomationOutputs/BattleRl/20261004_v39_4096_greedy_prefix_diagnosis.json`。

进一步确认原 `--fight-init-bias` 只改动作bp；在 kind→conditional tuple 策略中 fight 只有一个元组，该组内常量偏置被条件softmax抵消。修正为 Policy.bias_kind：分层时只给实际fight类型bk一次偏置，平坦时仍改各fight动作bp。

独立数值验证：旧元组偏置1的fight概率保持0.5；修正类型偏置1后为e/(1+e)=0.73105858；正式掩码禁止fight时仍为0，rest两元组各0.5；平坦策略仍为e/(e+2)。证据 `AutomationOutputs/BattleRl/20261004_factor_fight_bias_check.json`。不修改奖励、敌人、费用或游戏规则，不强制策略代打。当前v39实际偏置0，运行代码已加载且manifest保留原哈希；新修复为下一轮准备，不因检查点评估差就终止或重复启动当前活训练。下一轮仍须实际评估与消费者轨迹证明效果。
