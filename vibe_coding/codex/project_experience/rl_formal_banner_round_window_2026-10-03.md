# 轮末附旗选择必须在正式窗口

不能在回合开始预设取回、不能直接把旗从单位拷到牌库，也不能自动EndRound隐藏决策。先正式EndTurn与宣布轮末，Game.CanChooseRoundBannerReturns才授权选择实际自有附旗；原ChooseBannerRoundReturn仅预留选择，EndRound处理物理归还，再真实洗牌/夜轮开始。

窗口仍显示最终战斗历史，它不是下一场：必须保留原敌实体/击败标记，完成roundFinish才BeginTurn。策略观察应包含窗口与取回标记；合法操作限制为实际轮末选择/完成。次数清理与持久玩家、伤牌、单位状态仍走原Engine。

抽取回合开始代码时注意遇敌序号原本在Turns++后取Turns-1；若提前缓存，必须使用递增前Turns，避免悄悄重复上一组敌人。原132操作精确重放防止这种漂移。

证据 `AutomationOutputs/BattleRl/20261003_banner_round_window.json`（13/269，实际原勇气旗跨昼夜回洗抽回再附着），`20261003_planning_cost_exact_replay.json`（132操作，原字段与合法动作不变）。指挥旗单位移除特例及Unity回放另验。
