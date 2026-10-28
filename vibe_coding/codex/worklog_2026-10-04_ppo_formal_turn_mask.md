# PPO正式回合末动作掩码修复

## 问题与修改

真实新轮种子44642/44720/44534在无本回合实际出牌、全手伤牌的战斗结束后仍获得nextTurn“合法”动作，正式GameEngine正确拒绝。旧失败全部保留。将GameEngine原有回合末前置断言原样抽成无状态修改的ValidateEndCurrentTurn，BeginEndCurrentTurn仍调用同一断言。训练入口战斗结束与战外统一调用正式验证，不再维护一份近似义务条件。无合法后续动作时显式no_legal_continuation；审计保持缺口而非记为规则或模型通过。没有禁止合法战斗，没有给伤牌额外弃牌许可，没有修改原费用或胜利规则。

当前v40 PID125764继续使用冻结v39 DLL，未终止、未重复启动。新构建v41仅验证，待当前训练自然结束后依据评估决定后续训练；本轮累计历史不能跨DLL无条件续用，旧证据保持版本归属。

## 验证

- BattleRl和RulesCli独立输出构建均0警告0错误（退出0之后才执行）。
- 三条真实失败前缀157步，15次合法nextTurn完整状态/奖励不变；终止状态除显式stopReason外原实体/资源/伤牌不变，被拒绝nextTurn移出掩码，直接请求仍拒绝。3680审计断言0错误，3个no_legal_continuation缺口保留。`AutomationOutputs/BattleRl/20261004_v41_formal_turn_mask_check.json`。
- 真实共用测试：TurnMinimumActivityTests/PveJourneySessionTests/OriginalSiteTurnTests 22/22。`AutomationOutputs/RulesState/20261004_070105_30874d9b/summary.json`。初次选择命名空间错误退出1，不计测试证据；更正正式名称后执行上述检查。
- Unity脚本刷新等待idle，Console按1000条和堆栈两次0错误：`tmp/rl_v41_turn_mask_console_20261004.json`。首次refresh超时、编译期间端口拒绝均不当作成功；通过实际Unity窗口CtrlR和恢复后的refresh取得idle再读取Console。
- 未修改场景布局。本次是入口校验与掩码修复，不代表保存策略Unity回放通过、训练胜率提升或完整范围完成。
