# 2026-10-01 PPO正式覆盖范围纠正

用户要求所有战斗、英雄和牌库要素都在训练实际使用，非穷举并包含多轮。承认旧pilot仅接通协议，不能宣称通用目标完成或算力结论可靠。

新增 `tools/BattleRl/CoverageInventory.cs` 与 CLI `--coverage-plan`，从本地原始JSON、正式英雄/敌人loader导出原ID、模式、归属、敌能力和未覆盖状态。没有改正式规则、Unity源或训练模型。生成 `AutomationConfigs/rl_full_coverage_inventory.json`：7英雄、69敌、28基础行动、44高级行动、24法术、25神器、30部队、70技能。

第一次导出magic为0，因为法术没有card_name、使用spell_top.name。修复正确原名读取并重新导出24，避免把漏目录当作范围不存在。清单不增加任何训练或验收通过数。

正式执行/验收契约见 `combat_ppo_full_scope_2026-10-01.md`，明确欠覆盖采样、合法二元覆盖、实际出牌及消费者、连续正式回合/轮次、实体保持与未见组合评估；旧模型与时间证据保持独立。

构建与生成成功，Unity Console `tmp/rl_coverage_scope_console.json` 连续两次 error/count1000/includeStacktrace 零错误。目录生成和说明不修改测试场景，不重复训练或重跑已通过的Unity回放。无下载、上传、LFS、推送、系统设置变更。
