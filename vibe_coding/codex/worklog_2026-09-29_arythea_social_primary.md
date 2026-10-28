# 阿瑞希亚本人秘密谈判、红激励连续消费者

新增独立 `PveArytheaSocialPrimaryJourneyTests.cs` 与 `tools/build_pve_arythea_social_primary_acceptance.py`，不修改生产规则。使用原阿瑞希亚16牌和合法等级6两自身技能；保留既有演练构筑红蓝各一晶、原种子0，不改原卡、fixture或牌序。

第一回合本人激励实际抽两牌及红标，原032攻击4对护甲3产生真实两伤，原灵活战斗强化攻击4消费红标而保留红晶，击杀后正式结束。第二回合保留两伤，用原行进2付邻接平原进入村庄；秘密谈判昼2加激励所抽原狂怒横置影响1，支付3买治疗并移除一张真实旧伤。最终原16牌加一伤分布手6、库8、弃3，同一玩家与位置保留。地图为既有自定演练布局，不冒充原板块探索。

后台初次 `20260929_182938_ddc59fc3` 失败因测试沿用托瓦克原狂怒ID，阿瑞希亚原起始牌此处为灵活战斗024；改用原定义及真实 strong_attack。第二次 `20260929_183114_13350633` 本方法最后身份断言失败：DeedCard的Contains按同名比较，两张伤都被计入；改为ReferenceEquals验证牌实例，未改规则/期望数量。同批勇者方法已通过，未重跑它。

仅补本方法并检查新增Goldyx方法的 `20260929_183304_2f681967` 2/2通过。阿瑞希亚最终检查真实16牌全体引用保留、仅一原伤仍在、实际费用及正式两回合。四个新测试Unity导入编译后 `tmp/pve_social_assault_compile_console.json` 两次0错误。原失败报告保留。

独立场景计划59操作113断言3图。实际场景结果另行追加；后台通过不替代场景。

## 实际场景已通过，最终Console待统一收尾

`AutomationOutputs/PveBattle/20260929_183516_348eea5e/report.json` 实际success，59操作113断言；主代理已目视17/28/56三张关键图正常。逐状态核对：17步原激励实际抽2得红标；28步原024强化攻击4消费红标且库存红晶1不动；47步本人秘密谈判影响2，配实际原狂怒横置1后53步支付3治疗，56步真实旧伤2→1；58步同一Arythea两回合手6/库8/弃3、原16牌加伤1。

索引 `pve_arythea_social_primary_acceptance_index.json` 保留有效后台本方法1项、旧两次失败及实际报告/XML哈希。最终运行后Console暂pending，等待主代理全部当前场景结束后统一提供 `tmp/pve_social_assault_final_console.json`；不拿此前编译双0冒充最终检查。英雄账本000:03/08已经补入实际场景证据，其他代理条目未改。

## 最终Console已完成

本批四场景结束后，实际读取 `tmp/pve_social_assault_final_console.json`：两次 status=success、result.success=true、data=[]，确认两次0错误。对应本路线索引 finalConsole 已由 pending 更新为 passed 并记录文件哈希；前述 pending 为过程记录，不代表当前仍待检查。未改英雄账本或共享最小差集。
