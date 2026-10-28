# 法术熔炉蓝费与魔力强化奖励联合守恒

固定5000实验run_recovery1在第15批采样阶段因审计误报自行退出。最后完成14次更新，next_episode5402，total_steps289608，Adam855；117参数/动量数组、主RNG和32worker RNG均在真实checkpoint中。未完成批保存124局及worker10 episode5412失败，共1916训练归档，不冒充15更新。

失败task cbe91af69accccd1bc8a06ef5b74e71b2a10826275af596c4f823ef1d8e4621b，seed628379349，步骤77：法术熔炉强化选择magic_012/005。实付蓝token1，牌面奖励蓝绿各一晶，克朗魔力强化另奖蓝晶，故蓝晶0→2、绿1→2。对照resources/text_json/advanced_card.json与skill.json及SharedSkillArea.ManaPayments.cs，属于Python审计漏算共享技能奖励，未修改原规则。

仅在原强化需付款、技能真实拥有且本轮未用、蓝预选解除、使用标记上升、精确本人蓝共享token新增且无旧token删除时，增加一颗蓝晶期望，仍联合检查实际费用和溢出。原162步正式reset/step的完整前后状态、动作和raw reward精确相等，3870检查通过；漏付/多奖/漏奖/错色/缺发布/重复轮使用六负例均检出。原失败保留，报告run_recovery1/spell_forge_repair_gate.json。

新增显式--prior-run固定实验续训：校验父checkpoint路径、完整metadata、episode/steps/Adam计数、批量/奖励/评估设置，继承实际完成更新号和指标时间，只运行剩余15—100更新。父全部文件不覆盖；partial batch不冒充已优化，后续从父保存RNG重采样，旧失败批仍在父目录。沿用原基线，不以第14次权重重算冒充update0，第20/40/60/80/100评估保持不变。恢复与评估相关4测试通过，Unity Console两次error0。

确认旧训练/仪表盘退出后从同真实checkpoint启动run_recovery2，PID55060；未更改训练超参数和任务池，启动后仍需通过恢复门禁。保留归还骰提前nextTurn适配缺口，胜率不替代全规则和Unity验收。固定验证基线133/1000，仅此尚无学习提升证据。
