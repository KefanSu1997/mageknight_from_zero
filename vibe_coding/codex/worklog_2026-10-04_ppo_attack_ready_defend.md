# 实际攻击来源后的防护规划窗口

## 问题与最小修改

真实v50 seed77972轨迹43次防护选择/清除，其中39次尚无任何实际攻击贡献。这是合法入口允许的规划循环，不是原规则错误。仅调整训练与Demo的交互时机；不改原防护值、费用、敌护甲、攻击成功条件，不替模型选牌或目标。

`tools/BattleRl/JourneyTraining.cs` 的防护合法动作生成，在所选目标存在正式 `AvailableAttacks` 后才提供防护选择；仍允许取消已选防护。不要求攻击足够击杀，正式规则API保持不变。

`Assets/Scripts/Battle/PveBattleController.EnemyDefend.cs` 在打开防护面板前核查用户本次实际 `SelectedAttackParts`，空选择给出先出牌/选择来源的反馈，拒绝时不修改战斗状态。有来源后的既有面板及结算照旧。

## 后台与场景证据

独立构建 `tmp/battle_rl_v51_attack_ready_defend` 零警告/错误。seed77972前12次真实操作逐状态重放、目录/特征完全一致；仅在反事实分支跳过旧空规划/清除两步，后续真实牌/部队/取骰状态再次完全相同。有来源后的防护选择和取消仍合法，共42检查，见 `AutomationOutputs/BattleRl/20261004_v51_attack_ready_defend_preflight.json`。反事实不算实际训练恢复或胜利。

实际Unity定向报告 `AutomationOutputs/PveBattle/20261004_171123_a9f68228/report.json` success，45次UI操作。原Wolfhawk合法等级6、六原牌、原057+双032；空攻击结算拒绝，手6/红蓝晶各1/贡献0/击杀0/防护未应用。真实迅捷反应远程1、利斧投掷蓝费远程3、火焰箭矢红费远程3，合计7；原057为一个032提供防护1，护甲4+3，正式贡献7消费并击杀两实体，名望35→40（既有利斧奖励+1）。人工查看空攻击拒绝和实际击杀关键截图，未截断。此为定向原战斗UI验收，不是地图覆盖或保存PPO策略UI验收。

Unity Console编译后与场景后分别两次零错误：`tmp/rl_attack_ready_defend_compile_console_20261004.json`、`tmp/rl_attack_ready_defend_post_scene_console_20261004.json`。原场景请求按原字节恢复；新请求在本地 `AutomationOutputs/BattleRl/20261004_v51_attack_ready_defend_ui_request.json`，未推送/上传。

## 新训练轨迹审查与现状

v50新seed79595原傀儡大师保存真正击败的monster012物理instance2。原护甲5/火攻4，经过三次正式回合转换仍保存同实体，第4回合消耗产生物理格挡3；不因火攻误记为火抗。两次发动分别正确消耗当回合次数且无牌/魔力费。12项定向与2430项通用检查零错误，见 `AutomationOutputs/BattleRl/20261004_v50_actual_puppet_keep_cross_turn_review.json`。策略随后直接推进，未实际使用新增格挡，明确保留消费者缺口；不能将生成贡献计为格挡成功。

PID240872/启动15:59:07仍活，冻结v50未被改动或重复启动。最新10240/16384训练步、168episodes；审查10591检查零错误、3缺口。8192保存模型评估0/7完整胜利。新v51入口尚未进入活v50。原要素完整真实消费、未见构筑、保存策略Unity回放及可用胜率仍未完成；目标继续。

## 傀儡消耗后的合法动作诊断

继续用同冻结DLL重放seed79595前41次实际操作，82个完整前后状态完全一致；诊断证据 `AutomationOutputs/BattleRl/20261004_v50_puppet_decision_diagnosis.json`。总物理格挡4，原055迅捷物理攻击3要求6、原068经实际笨重消费后攻击7要求7。此时正式mask没有任何block/blockSelected，唯一合法动作advance。因而不能把紧接着推进阶段描述成跳过足够格挡。

8192步保存权重在使用傀儡之前的同状态：puppetUse概率0.971563，advance0.028437；使用后advance概率1。这是保存检查点诊断，不是当时实时权重。可用模型的规划缺口应定位到更早消耗傀儡、产生不能挡住当前任何攻击的资源；规则API没有错，不应禁用这个合法但无效的操作来冒充策略改善。已产生贡献仍不等于成功消费者。
