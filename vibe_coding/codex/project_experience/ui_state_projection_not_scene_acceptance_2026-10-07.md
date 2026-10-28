# 正式状态到Unity自动化字段映射不等于场景通过

新增ui_state_mapping.py，按现有PveBattleController.CaptureAutomationState源码，映射共享战斗字段：阶段/胜利、英雄、名望、手牌/牌库、格挡影响、基本晶体/六色token、单位ID/数量/ready/伤及取骰额度。目标为后续逐操作实际Unity capture比对，缺键/值不同均报告，不把生成期望当实际UI。

Outside或非inBattle明确拒绝，因为Unity使用另一CaptureJourneyState路径；UI选择、素材、攻击来源引用/状态外隐藏区域未虚构映射。费用实际47步Tovak片段测试Blue token1→0、Red3、出牌离手，错误Blue与空capture拒绝，两项检查通过；Console两读0错误tmp/rl_ui_projection_console_20261007.json。未运行Unity场景，不宣称截图/完整UI验收。

并行四个既定新敌train构筑、不同英雄，冻结152局模型各四次探索仍零完整见证，所有失败/部分胜利保留unknown。20261007_new_enemy_hero_gap_queue.json原定义/分区不变，20261007_new_enemy_hero_policy_discovery会话50561正常完成，未增加已训练敌人覆盖。

仍需 actual Unity捕获及控制映射、多组稳定成功、更多原敌/构筑/模式费用消费者；代码比对接口通过不能代替最终目标。
