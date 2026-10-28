# PPO 原部队接口与完整战斗胜利评估

## 上批评估已结束

原v12新种子评估483情境完成，复用已持久前138条汇总并补测无持久结果的13条，未重跑已保存138条。最终至少赢过一整场152/483=31.46998%；真实完整清场298/1409次开战=21.14975%。目录AutomationOutputs/BattleRl/20261003_v12_true_battle_evaluation_resume。前138只有总汇总，英雄/击杀/伤牌/停止原因等明细和审查数量仅新345条，manifest/detail_scope明确，不能外推为全部483明细。新345条341102项已有独立断言零失败，但6条触及适配器已知缺口，14条未完成战斗，299条到夜间、122条正式完成两轮。不是所有规则/UI已验证。

## 原部队v13

tools/BattleRl/JourneyTraining.cs新增--serve-journey-units独立协议；加载UnitJsonLoader.SupportedIds全部30原定义、原能力和费用，不生成替代规则。使用明确的中盘自有库存快照，每个玩家仅1支原部队，合法占用原等级1指挥槽；不把该快照说成标准开局招募或获得流程已验证。原16/21/26牌库存仍沿用。魔宠按正式PrepareRound与Game.ResolveFamiliarUpkeep消费真实库存晶体；库存无可用晶体则正式去除，不制造维持资源。轮次转换也走同一维持入口。

动作：正式UseUnit/UseOutsideUnit，各打印能力索引、合法二选项、红蓝额外付款、自选魔力颜色、敌人与攻击槽；实际PlanDamage预留与取消、实际治疗英雄/单位创伤。观察追加实际部队就绪/伤牌/颜色、大师能力已用记录、伤害预留与攻击贡献单位来源。实际消耗与状态按正式规则保持跨回合/轮。静态词表按能力真实目标需求生成，普通攻击/格挡不重复枚举无意义的敌人/攻击槽。

所有新能力合法性仍通过正式入口预检和完整checkpoint恢复，未绕过耗竭/伤势/付款/阶段校验。单位攻击贡献按真实SourceUnit绑定原ID与最后发动能力，coverage的units/unit_abilities及后续攻击/承伤/治疗消费者单列，不以加载或初始自有冒充覆盖。

## 验证

初版DLL tmp/battle_rl_v13_units/BattleRl.dll烟测1024步62.10秒（16.49步/秒），2340观察维度、17518动作。实际发动9原部队10能力选项，七英雄小样本尚不能评价胜率。输出20261003_units_ppo_smoke。静态目标枚举收窄后独立编译到tmp/battle_rl_v13_units_checked，不覆盖运行中DLL。初次DamagePlan字段误用EnemyIndex导致2错误，读原UnitDamageAllocation后改为TargetIndex，最终零警告零错误；未修改Assets。

verify_unit_inputs.py用真实seed2911、原army.json的army_003（埃莫提普烈焰枪手）检查红费火焰远程6、实际SourceUnit贡献、耗竭不能重复发动、伤害计划确认前不改创伤/就绪、真实承伤、下回合原伤保留且耗竭不刷新，以及另一条轨迹的免费格挡5。8定向断言+154逐操作断言零失败，证据20261003_original_unit_inputs.json。Console两次零错误tmp/rl_unit_interface_console_20261003.json。仅工具接口与后台证明，尚未进行这个模型范围的Unity UI操作回放。

## 当前训练

从已结束1024步烟测权重语义迁移（删掉无意义重复动作，旧匹配权重按Key复用），用checked DLL续训16384步，下一种子2833，协议--serve-journey-units；目录20261003_original_units_ppo，日志20261003_original_units_ppo_stdout.log，session28529。7英雄checkpoint评估、每条完成轨迹检查、约60秒保存新实际训练轨迹，首次部队攻击消费者另留一次原始轨迹。遇到独立断言或未预期协议错误保留失败后停止。

## 未完成范围

只有1单位库存，不足以训练重整另一单位、协同加成、不同单位顺序承伤和全部技能部队目标；单位正式招募、斥候实际地图/标记目标、战术前维持由模型选择仍未接入；本接口不支持完整地图场地。全部神器、部分法术/技能专用输入、多玩家/复制材料与超过8个攻击贡献仍有缺口。完整目标仍active，不把9单位烟测或新词表当30单位所有能力消费者已通过。
