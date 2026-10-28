# 实体token只能从公开观察取值，原目录数量不能猜实际坐标

新增 `public_entity_tokens.py`，只接收已有6314维有限数值公开features，不接收State字典、seed、audit牌序或物理实体审计字段。当前固定world featureSchema之外拒绝执行。它为原卡手牌ID和数量、本人技能次数、部队公开状态/槽位、敌人原ID/目标槽/能力/攻击/有效护甲/有效城防生成350维实体token，并以明确global token允许缺少其他实体的样本。原完整flat观察必须保留，token是补充，不能凭ID计数替代真实手牌槽、付款、pending或历史义务。

初稿失败必须保留：清单敌人键是Id而非id；原库存高级行动44，但运行时原JSON数组有48行（044–047仅ID/图元数据，无印刷效果），部队原JSON两套各16而正式SupportedIds各15合计30，且顺序不是简单按ID排序。直接按目录44或JSON32去猜坐标会错位。修正读取正式部队SupportedIds顺序、正式monster loader同样的有attack/armor过滤、技能清单英雄顺序对照OriginalHeroes源；原高级44 token使用源数组48造成的真实后续偏移，044–047手牌非零时明确拒绝/保留来源缺口，不造原卡文本或把占位行纳入原覆盖。失败摘要另存，未对初稿错误坐标跑训练。

独立正式后台重置Goldyx/Tovak/Braevalar，各取Outside与真实合法fight后状态，6新投影状态758检查通过：手牌数量、技能所有权和次数、部队ready/wound/destroyed/槽位、实际敌人原ID/甲及有效甲/城防吻合。State仅作独立测试oracle，token方法只能看已有features。对照同时运行Transformer有限前向/反传，不改变原环境或模型。新增3输入合同测试通过：错误schema/长度、NaN、敌人ID不明确、部队槽不明确、占位原卡坐标皆拒绝；原ID计数和次数坐标正确。

证据：`AutomationOutputs/BattleRl/20261008_public_entity_token_projection/report.json`。仍未把token接入三层MLP+GRU、PPO序列更新/检查点/真实训练；这里只是新实体输入适配门禁。未重跑全卡库或旧成功链，未记录新PPO覆盖。Unity编辑器仍关闭，Console两次零错误和保存策略Unity操作仍pending，不以Python通过替代。
