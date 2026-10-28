# 持久翻面恢复条件与洞悉真实消费者

- 战斗狂乱翻面必须存于真实玩家，不能放临时Context或每轮自动清理集合；只在成功正式休息后恢复。萨满单独ManualFlip频率，以本回合行动恢复，与休息互不替代。普通StartTurn/PrepareNewRound不清持久标记。
- 特殊恢复操作需允许从已翻面状态进入，但普通取魔仍拒绝；先验证实际行动窗口再修改，TurnActionCommitted继续交给正式战斗/移动/交涉消费者约束。原六色标记不转换成元素、不当魔晶。
- 洞悉动态选择真实敵标能力或一次攻击去色；能力影响DefenseProfile及CombatMath抗性/城防，不能只改显示。技能Session须在执行前保存选择的label，执行后该能力已忽略、动态选项会消失；不能再Single查询已消失选项。
- 敌人原能力仍可展示来源，但技能生效后必须标“已忽略”，否则原图/文字和实际格挡需求看似矛盾。截图捕捉到这一问题后，仅补显示受影响战斗段。
- RulesCli同程序集可调用internal方法，不代表Unity测试程序集可用。测试通过正式PlayerTurnEngine与Journey.PrepareRound核对持久状态；单构造Journey并不实际开始轮次，不能作为轮次测试证据。
- 原Regenerate数据无消费者且有原图识别疑点，保留明确缺口，不能因为枚举存在就给洞悉伪可选入口。

结果与范围：`../worklog_2026-09-29_flip_insight_skills.md`。
