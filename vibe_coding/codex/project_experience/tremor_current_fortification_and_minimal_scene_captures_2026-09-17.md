# 震颤／地震：按当前城防计算、叠加减甲与最小必要场景图

原卡`Assets/GameData/cards/magic_en_003.png`要求单体减3/全体减2；强化对当前有城防敌人翻倍。不能只检查怪物静态Fortified：地点城防、拆毁忽略地点、暴露移除单体城防都会改变结果。`CombatMath.IsFortified`与攻击限制共用当前有效判断，双层城防仍保留独立限制。

减甲累加，不覆盖之前震颤或拆毁；最低护甲1仍由正式CombatMath计算。单体非法/已败/奥术免疫在付费前拒绝；全体跳过已败/奥术免疫。火冰抗性不能阻止绿色法术。保持正式EndTurn期限，不因战斗结束提前清除减甲。

测试必须消费减甲后的真实攻击并检查实际击杀；尤其先拆毁后地震与先地震后拆毁结果不同，暴露单体不能改变另一敌人的加倍资格。后台30个新参数例与8个相关回归先通过，再运行37个场景例。证据`AutomationOutputs/AllOriginalCards/20260917_044418_fd5b233b/magic`，867效果检查及226UI步骤符合；保留25例partial。

定向场景请求仅保留每例execute最终对照截图，所有实际按钮和数值断言仍执行。`SceneAutomationOrchestrator`用`skipScreenshot && passed`确保任何失败步骤仍截图。37例仅38张图，避免225项请求步骤各存整屏图。最终图必须实际检查原卡、各操作实际/预期、费用/卡区等；信息不全时仍须补充必要中间图，不能一律省略证据。

域重载中的连接拒绝先等待、refresh恢复后重试，不快速重试或重启。场景前后Console分别两次0 error。详见`vibe_coding/codex/worklog_2026-09-17_tremor_fortification.md`。
