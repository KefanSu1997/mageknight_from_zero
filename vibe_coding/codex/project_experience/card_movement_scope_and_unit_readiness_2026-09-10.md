# 原卡移动范围和部队重整经验

## 用规则目标替代错误的内部状态假设

德鲁伊之道写的是某格/某类地形减1最低2。旧场景期待TerrainCostOverride:Forest=2，却会诱导实现成全局绝对覆盖，错误影响基础效果的其他森林格。本轮保持独立数字2，改查MovementService.GetCost，并增加同地形另一格、连续移动、不同昼夜、最低费用和山脉不可通行检查。更换错误观察字段需要同时记录原卡证据与新增反例，不能用实际结果回填预期。

地图示例目前按MapTile.Edges[0]代表格地形。折扣单独存储到HexMoveReduction与TerrainMoveReduction，共用成本入口供实际移动消费；既有TryMove调用约定不擅自双扣，新增TryMoveUsingPool仅在实际移动成功后扣池并更新CurrentTerrain。

## 单位就绪与受伤是独立状态

官方规则书第4、10页：https://www.mageknight.net/wp-content/uploads/Mage-Knight-Board-Game-Rules-2012-March.pdf

重整只移动指挥标记；伤牌保留且阻止发动。治疗也不自动让耗竭部队就绪。普通回合开始不能代替新昼夜轮次的全体重整。CanActivate集中检查就绪、无伤牌且未摧毁；不能只看IsReady。无情威压强效重整限自有1/2级、每级2影响力，失败不能消费资源。

宁静时光的回合行动转换没有该等级限制；不得把两卡混成同一权限。后续必须为其单独验证回合行动占用，不能只设置数值字段冒充联验。

## 观察真实后续动作

出牌与后续每次移动/重整/回合结束各有独立按钮点击与报告状态。禁止在断言层补出没有执行过的费用或位置；报告明确区分操作被合法拒绝与自动化找不到按钮。部队完整能力、伤害、控制权与卡区生命周期仍需继续验证。

## 编译经验

引入MK.Logic.Runtime.Map后，Monster与MK.Logic.Data.Monster重名。Console报CS0104时只增加明确类型别名，重新实际编译并两次读取Console零错误，再启动测试。没有必要改动两套领域模型或项目packages。
