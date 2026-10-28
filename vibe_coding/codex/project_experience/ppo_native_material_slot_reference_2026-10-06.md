# 同名材料的手牌位置必须按物理引用查找

新实际seed404831魔咒付款选择伤牌34，正确弃掉实体34；报告materialSlot=5却指向同名伤牌33，真实手牌位置应是6。原失败断言和完整前后状态保留 `AutomationOutputs/BattleRl/20261006_v88_material_slot_failure.json`，原完整前缀不改写。

`DeedCard` 是C# record，`List.IndexOf` 使用值相等，找到第一张同名伤牌。`JourneyTraining.NativePayments.cs` 唯一修正：材料位置改用 `FindIndex(c => ReferenceEquals(c, s.Material))`。已有actor付款特征按ReferenceEquals生成，不能把审计位置字段错误说成模型选错实体。修复须独立重放原失败，对照原/新策略观察、完整掩码、动作词表、正式状态和原奖励；只允许materialSlot元数据按实际物理ID纠正，不放宽失败预期。

新DLL与旧DLL不同；不得伪造manifest继承旧使用历史。旧历史留原目录，新轮重新统计实际使用，已保存权重仅在schema/真实输入不变证据成立后复用。最终真实Unity策略验收不因CLI元数据修复通过而完成。

实际修复验证完成：新旧DLL完整123操作配对，17次材料引用状态核验；仅第9步付款后元数据materialSlot5→6纠正。actor features、完整动作词表/合法mask、原正式状态（除该字段）、原reward、done、schema均逐步完全相同；旧错误位置负例检出。报告20261006_v89_material_reference_replay.json。构建0警告0错误、Console两次0错误tmp/rl_v89_material_reference_console_20261006.json。新DLL覆盖历史不继承，旧报告保留。
