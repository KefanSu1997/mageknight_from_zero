# 实体6/99对普通2/99仍弱，真实漏计修复不能冒充学习

首次同1120父状态、同238池、LR0.0001、两轮8局序列PPO配对，普通与实体的全部543真实操作链相同，两支0整任务成功，实体13组注意力实际更新。各冻结33个development组成×3个actor seed：普通0+1+1=2/99、实体3+1+2=6/99，有限检查分别184381和172887均零错误，参数冻结/零优化器更新。存在弱开发校准信号，但单训练流、重复调参/33组成小样本不能证明可靠泛化或正式晋升。30最终test、全部原要素/费用消费者及Unity仍未达到。原结果 `AutomationOutputs/BattleRl/20261008_entity_pair_full_frozen_comparison.json` 保留；不要把99说成99独立任务。

已知实际覆盖漏记不是规则错误：旧collector遗漏worldFly的真实轮标记；黛尔法纳大师可耗竭后同次激活再付费使用不同能力，仅看ready→false漏记后续模式。修复要求飞行本人round marker实际新增；unit事件由ready真正消费或与action.Choice一致的新usedAbilities标记触发。预选/重复/其他选择不计数。忠诚特殊招募另以真实新部队/绑定槽消费发skill_consumer，不伪造技能翻面/次数激活。

三必要正负例通过。仅检查受影响的三个实际PPO旧轨迹1104/1106/1118（属于该实体actor实际父链且同冻结DLL），与保存旧collector逐行比较，补四飞行与四多能力模式，共8实际漏计；旧已有事件不删/不重复补。手动忠诚后台fixture只检消费者分类，未纳入PPO计数。原旧checkpoint/计数保存，新修正结构记录每条episode/step/task/source SHA。实体37族参数/Adam共111数组、RNG/step/episode/任务访问保持精确相等，仅collector SHA、带证据覆盖统计更新。未来覆盖缺口采样权重可因此改变，必须明确是统计修复而非policy学习。

证据 `AutomationOutputs/BattleRl/20261008_actual_coverage_collector_repair/report.json`，Python编译通过。修改发生在训练/冻结进程全自然结束后，不热换。未改正式规则/mask/raw奖励或放宽期望。Unity编辑器关闭，无Console接口，两次Console和实际UI门禁仍pending。全70技能不能用飞行+4或忠诚fixture补成已通过。
