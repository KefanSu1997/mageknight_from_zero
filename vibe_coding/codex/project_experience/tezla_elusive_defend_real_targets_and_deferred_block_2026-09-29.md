# 原双甲、防护与正式格挡结算

- 原057不能仅用原armor字段1；官方Tezla p2/p4及原图确认Elusive高甲2/低甲1、Defend1。字段与能力身份共同进入正式Loader，显示/攻击共用EffectiveArmor。
- Defend分配按实际源/目标引用，一源一次、不同源不同目标；预选不变甲/耗资源，确认攻击才应用。已经应用的防护持续整战，即使来源随后死亡。不得用同ID判同实体。
- 两冰攻击须实际逐槽全挡；取消攻击/无伤不等于成功格挡。PveBattleSession.Block先存正式分配并消耗格挡，SuccessfullyBlockedAttacks在Defense结算写入，验收须在正确时点读取。
- 原利斧投掷强化一次攻击击杀至少1敌额外名望1，不应只加怪物印刷名望。依据原卡修预期，不能改规则迎合预期。
- CLI同程序集可见internal而Unity测试程序集不可见。新测试须通过正式英雄获取/UseSkill路径，不为测试开放internal。两个每轮一次分支各自独立合法玩家，不伪造次数。
- 新遭遇外部编辑可能触发Reload模态并阻塞MCP；先看实际窗口并加载，再等域重载恢复，不重复compile/启动。

证据：`vibe_coding/codex/pve_elusive_defend_acceptance_index.json`。本批后台11有效、连续65操作，原失败保留，分段复用通过前缀。阵营奖励和其它资料依赖继续明确未完成。
