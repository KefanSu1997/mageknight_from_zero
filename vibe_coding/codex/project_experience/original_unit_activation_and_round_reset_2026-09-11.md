# 原部队能力、费用和每轮重整

原始部队JSON不能直接视为正确规则。army_003实际原图存在完整能力但JSON只有路径，守护者魔像缺物理抗性和招募图标，僧侣招募地点错误。先读Assets/GameData/cards原图，保留原文，再恢复数据，不能以空定义或猜测能力通过验收。

UnitAbility把印刷文字、互斥选择、元素、攻击方式、魔力费用和自伤分开。UnitJsonLoader目前只允许8张已核对定义；遇到未接入能力拒绝整张载入，不把剩余文字静默忽略。CardUnitActions验证实际所有权、就绪、伤牌、销毁状态、能力选项，再整笔支付魔力；成功后才生成资源、横置并处理自伤。

普通回合与一轮不能混用。旧TurnEngine每回合调用UnitState.NewRound导致重复发动，已删除；正式GameEngine.StartNewRound才恢复指挥标记。伤牌独立于就绪状态：新一轮重整不治疗，治疗不自动重整。跨回合场景使用实际两套回合入口，并在新轮重新建立ActionContext与交战状态，不能把过期攻击力算入期望。

招募必须走实际供应列表与RecruitmentService，只有成功才支付影响力并移除供应。农民产生影响力后继续招募第二支农民，可证实资源确实连接后续操作。乌顿剑士主动自伤后跨轮拒绝发动，可证实不是仅改变UI数字。

验收：AutomationOutputs/OriginalUnits/20260911_193332_c99cb476，110例、612操作、2937断言、213截图；EditMode212通过；技能39例与四冒险场景回归均通过；Console双零。只证明列明招募和主动能力分支，不代表部队分配伤害、抗性承伤、全部阶段限制及其余22张有效部队完成。

复现：退出Play Mode后运行 python tools/restore_original_unit_data.py、python tools/build_unit_verification.py；Unity菜单 Tools/Mage Knight/Original Cards/Run Units；python tools/verify_unit_scenes.py --output <实际新运行目录>。不使用batchmode，不安装包。

存档经验：完整回归截图每批上传会消耗大量代理流量。保留本地证据和指纹，推送前核算新增LFS对象；不能仅根据代码diff很小判断git push很小。当前已暂停远端推送。
