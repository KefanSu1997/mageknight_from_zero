# 愤怒号角：额外攻城、正式骰面与真实创伤

原items_009普通攻城5并掷一骰；焚毁攻城5加自选0至5，每增加1投一骰，红/黑各造成一张创伤。焚毁选择0不投骰，白昼黑色也造成创伤。复用现有WrathHornEffect/CardPlayActions，未改正式规则或模拟骰面。

PveBattleSession验证阶段与原卡身份，PlayHorn转交正式出牌，回显实际LastManaRolls与伤牌增量。Controller只在焚毁显示0..5选择，普通固定额外0；原卡普通进入Played、焚毁永久移除。真实伤牌加入手牌并保留至战斗结果，攻城可穿城防且在近战有效。

BattleLoadout的随机重放开关默认关闭；专项验收启用种子并在场景明确显示。种子0前两骰金、金；6首骰黑；2前五骰金、绿、红、黑、红。种子注入现有EffectRandom，不替换正式随机消费者。普通演练仍使用正常随机源。

后台PveWrathHornTests十例首次通过，覆盖额外0..5、普通两骰结果、非法额外/卡身份/阶段的原子拒绝与近战；六场129真实按钮点击411检查首次通过，九张关键截图已核对，最终Unity Console两次零错误。证据pve_horn_acceptance_index.json；生成器tools/build_pve_horn_acceptance.py。焚毁1/3/4仅后台，未声称穷举所有骰面组合或完整回合流程。已通过范围退出日常队列。
