# 迷雾形态：从牌面状态到实际承伤，再到回合失效

- magic_014 强化原文是“所有部队获得所有抗性、英雄忽略敌人首伤及附加效果”。旧效果写 FirstWoundIgnored，而 BattleResolver 读取 IgnoreNextWound；仅检查设置成功会掩盖消费者未接通。必须从 ActionSystem.Play 进入 BattleResolver，核对真实英雄手牌、弃牌伤牌、原部队伤牌和销毁状态。
- 全抗性应参与 UnitDamageResolver 的匹配判定。抗性先吸收一份护甲，剩余伤害才造成一次部队伤牌并继续扣护甲；不能把全抗性当无限免伤，也不能消费属于英雄的首伤保护。冷火攻击也必须覆盖。
- 首伤减免在 AbilityRules.AssignWounds 前扣除，因此那张被忽略的伤牌不会触发毒素/麻痹；剩余伤牌和后续敌人攻击仍触发。非敌人来源伤牌不能消耗这个标记。
- EndBattle 与 EndTurn 不能混用。迷雾的回合效果在同回合下一战仍有效，结束回合才清理；SkipDraw 提前返回之前也必须清理。基础地形费覆盖和禁止丘陵山脉同样需要回合结束失效。
- 承伤后部队可能被销毁并移出玩家区域。场景保留原实际 UnitState 引用用于验收，不能把“列表第0项”静默当成之前的目标。其他旧例仍需做前后状态回归。
- 新增场景用例必须复用原卡SO／牌面和原部队JSON。费用不足、白昼强化、无效选项须在付费前拒绝并检查整笔资源守恒。截图文本完整性由实际TMP截断/溢出状态检查，另外人工查看输出PNG。
- 单卡新增用例通过不代表整卡完整通过：当前仍未覆盖完整牌区生命周期、湖泊不安全格的强制撤退及其他组合，必须保留 partial。

复现：`python tools/build_all_card_verification.py`；Unity 菜单 `Tools/Mage Knight/Original Cards/Run All Batches`；然后 `python tools/verify_mist_scene.py --output <本次真实输出目录>`。禁止 batchmode。源码、精简报告和哈希清单可本地提交；用户恢复远端上传前不推送任何截图或积压LFS对象。
