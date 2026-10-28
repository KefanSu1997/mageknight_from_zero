# Transformer实体编码必须先验证梯度与padding，模块不冒充训练对照

同规则、同238池、相同1104参数/Adam/RNG父检查点的16局配对，仅降低Adam步长0.0002→0.0001。首4实际native链完全相等，训练采样完整任务3对2不是冻结能力结论。后续固定33个development组成×3个actor seed，半步长为1+1+1=3/99，控制为0/99；单训练随机流、小样本、重复调参验证不能外推泛化，也未超过旧1104的3/99水平。模型不晋升，旧模型、失败、原任务支持和独立最终测试保持。记录 `AutomationOutputs/BattleRl/20261008_world_half_lr1120_full_validation.json`。

为推进用户要求保留的Transformer实体编码对照，新增独立NumPy `tools/BattleRl/entity_transformer.py`，不下载依赖、不替换当前三层MLP+GRU actor。模块提供输入投影、多头实体自注意力、残差LayerNorm、tanh前馈与masked mean池化，以及全部参数/输入解析梯度。存在mask只允许有公开global实体的非空样本；padding在矩阵乘前置零，连padding NaN也不得污染输出。实体排列置换不改变池化输出；公开槽位/实体ID如果需要影响动作必须由后续合法token合同显式提供，不能依赖数组排列猜测。

三项模块门禁通过：13个参数族的26有限差分探针及3输入探针、输入padding梯度零；实体置换前后输出/梯度和添加任意NaN padding后的输出/梯度一致；空实体拒绝，真实注意力Q/K/V及输入投影均有非零梯度。Python编译检查通过。不是原卡或敌人规则回归，未重复已通过原库。

仍需公开实体token白名单与原ID/物理槽合同、与当前三层编码和跨回合GRU的序列集成、注意力参数/Adam/RNG完整保存恢复、正式任务rollout与同预算冻结对照。模块不读取游戏state或audit字段；不把有Transformer代码当作已训练Transformer、已提升policy或完整目标。所有原要素/费用/材料/消费者、真实Unity操作仍未达标。当前Unity编辑器未运行，工具清单无Unity Console接口；两次真实Console零错误仍pending，不能以Python检查冒充。
