# PPO格挡来源与正式Demo操作映射

## 改动

复用原攻击来源面板结构，在现有 `PveBattleController.AttackSelection.cs` 内增加格挡来源面板；按真实 `BlockAllocation` 引用保存未选来源，仅列出当前敌人/攻击槽可用贡献，分页每页6项。确认调用既有正式 `BlockSelected`，有效值调用正式 `BlockSelectedPreview`。阶段重置、回合/战斗切换通过既有 `ClearAttackSelection` 同时清理格挡选择；仍保留原攻击操作。

`PveBattleController.cs` 仅在格挡且已选一个目标时提供分配入口，添加实际格挡来源与选择数量的自动化观察字段。默认全部当前合法来源参与，用户可以保留一笔给下一个攻击。每次成功格挡清理临时保留选择，下一目标依实际剩余贡献选择；未选物理实体不会被消费。

## 当前验证

初版与第一次文字修正各已取得Unity Console两次零错误（`tmp/rl_block_source_ui_console_20261004.json`、`tmp/rl_block_source_ui_fixed_console_20261004.json`）。真实自动化测试发现主界面TargetPreview两行文字高度不足，分别保留失败报告 `AutomationOutputs/PveBattle/20261004_163314_cca216a0/report.json` 和 `AutomationOutputs/PveBattle/20261004_163721_5d1229ee/report.json`。未删断言；现改为一行展示当前攻击序号/有效值/需求，面板中仍显示不足不能减伤规则。最新修改待重新编译及实际补测，尚不能标记场景通过。

定向请求在 `AutomationOutputs/BattleRl/20261004_v50_selected_block_ui_request.json`，三张合法Tovak原牌行进、决心、寒冰护体，原双032攻击各4：决心强化付款蓝晶后5、寒冰护体基础冰3、行进横置物理1。第一敌仅用决心5，其余4保留；重置本阶段真实恢复原手牌与蓝晶，重做后第二敌冰3+物理1格挡，正式防御结算伤牌0。该定向UI用例不是通用PPO模型完整验收。

## 训练

PID240872起始15:59:07仍确认存活，未重启。v50首4096步实际7英雄评估0/7完整战斗胜利；5120步新定时审查8666个通用检查零错误但3缺口，训练总计86episodes，覆盖计数不能代替全部原效果和UI验收。


## 最终定向场景通过

`AutomationOutputs/PveBattle/20261004_164446_4962ddfe/report.json` status=success，53次真实UI点击完成；未改变失败期望。修正主界面预览为一行后 `ui.truncated` 始终空。人工查看来源选择、阶段恢复、正式防御结果三张关键截图：决心5选中、冰3/横置1保留；阶段悔棋恢复三张同一原手牌及蓝晶1；重做后双原032均显示已格挡、伤牌0。第一/第二格挡后资源9→4→0，正式 `SuccessfullyBlockedAttacks=0:0,1:0`。

最终编译Console两次零错误见 `tmp/rl_block_source_ui_final_compile_console_20261004.json`；场景后再检查。原AutomationConfigs配置按原字节恢复，定向请求和实际报告保存在本地输出，不覆盖历史失败，不上传截图。当前界面功能已接入与定向场景通过；并非PPO完整模型UI验收，未见构筑/多地图/全原要素仍缺。

## 检查点策略诊断

同冻结v50 DLL重放seed75644前38步完整实际状态；4096步保存权重在该全伤手牌分叉的kind概率：rest0.791736、fight0.114347、source0.093916，greedy=rest。证据 `AutomationOutputs/BattleRl/20261004_v50_checkpoint4096_wound_only_decision.json`。这是保存模型对实际前缀的诊断，不能冒充历史实时策略概率或已实际训练的恢复行动。6144步累计102episodes，通用轨迹9267检查零错误而6缺口，整战评估仍0/7。


## 新实际选择格挡训练消费者

seed77465真实训练首次领导轨迹：原农民无费格挡2+原领导3=物理5，同非首农民耗竭与本人技能回合使用；原牛头怪物理攻5实际格挡且消费。石魔像原物理4/笨重逐笔实际移动2→1→0、攻击4→3→2，随后两原横置物理1+1实际blockSelected消费；后续黑夜同一存活原石魔像实体，原寒冰之盾基础冰3免费与横置1实际格挡4，基础不降护甲。22项定向检查及2182项通用检查通过，证据 `AutomationOutputs/BattleRl/20261004_v50_actual_selected_blocks_leadership_cumbersome_review.json`。两个真实blockSelected均选所有两笔来源，不能冒充训练已学会保留来源；独立UI的保留能力和策略实际学习分开。原技能/部队格挡来源元数据缺SourceUnit，整场won=false，保存策略Unity回放仍缺。


## 新轨迹规划循环定位

首6144步定时审查中的seed77972有43次敌防护选择/清除，其中39次当前没有任何实际攻击来源，最长连续规划23步；见 `AutomationOutputs/BattleRl/20261004_v50_seed77972_defend_without_sources.json`。这些是被现入口允许的反复规划，不能记成原规则违规或整战通过。下一步需要核查防护规划入口是否应只在真实攻击选择时开放，与正式UI发起攻击窗口对应，而非强行替策略攻击/改变原防护。未改动活训练的冻结DLL或重启进程。
