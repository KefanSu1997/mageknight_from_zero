# 原16牌实际UI入口与小牌库隔离

新增独立Run PPO Original Opening菜单/rl_ui_original_opening配置，沿既有实际菜单、Camp、原英雄picker选择Tovak、完整16牌战斗试玩、day_1、BeginTurn、Encounter0。八次原EventSystem点击与断言全部成功，没有设置手工假玩家/免费资源，也没有覆盖旧pve_battle.json或Build重建场景。

实际UI：hero Tovak，Level1/Armor2/CommandSlots1，Skills0/Units0，所有基础晶体0，手5/牌库11/总16；原玩家从正式战术与回合进入Ranged，同27规则/UI共享字段对照零差异。原hero图与实际5手/11库/零资源截图已视读。

hero.standardStart=False表示选择的是原16牌战斗试玩而非标准地图入口，不改false掩盖地图/世界尚未完整。只确认标准牌库和正式流程，不声称完整原版地图或保存PPO策略已通过UI。

证据20261007_unity_original_opening/report.json、opening_shared_state_comparison.json、captures/007_正式进入真实战斗.png。Console两读0错误tmp/rl_standard_deck_ui_final_console_20261007.json；真实场景正常结束。原规则/模型/JSON未改变，完整目标仍缺广泛消费者/69敌/独立测试与全策略UI。
