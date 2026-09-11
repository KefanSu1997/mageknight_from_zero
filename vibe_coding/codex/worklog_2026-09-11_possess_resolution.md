# 魅惑／附身：定向修复与场景验证

## 范围和依据
- 依据原卡 `Assets/GameData/cards/magic_021.png`、原 SO 与 `resources/text_json/magic.json`。
- 发现强效硬编码普通攻击5，原有独立预期为敌人实际攻击4；此次保留该预期并修复实现。
- 上批无新增实现，仅核实最小必要记忆落盘；本批继续失败用例。

## 实现
- 按所选敌人的原攻击数值和元素生成附身贡献，不复制特殊能力。
- 非法敌人索引在支付前拒绝。借来的贡献不能攻击来源敌人；普通贡献仍能使用，消费仅移除实际参与的贡献。
- 附身只能在近战攻击阶段使用，不被后续远程转换提前使用或消耗转换许可。
- 正式战斗结算入口拒绝非法来源目标，战斗结束清除附身贡献及其跳过攻击标记。
- 测试场景显示真实攻击目标、逐步攻击/格挡、失败后剩余近战资源及结束战斗操作。
- 新增按测试类运行 EditMode 的配置入口，防止隐式全量。

## 验证计划
- 原有606用例定义和预期逐项相等；新增20例，完整库626例。
- 本轮仅24场景用例：magic_021的22例及magic_010_01、magic_011_28两个直接影响回归。
- EditMode仅PossessResolutionTests、CardCombatRulesTests、OriginalUnitDamageTests。
- 41项定向EditMode通过、0失败、0跳过：`AutomationOutputs/EditMode/20260911_220656_4ca7d067`，已核对XML与摘要。
- 首轮场景 `20260911_220743_42666613`：24例中23例断言通过；新增magic_021_13误用小写discardWounds，唯一失败是无法找到状态字段。修为既有PlayerState.DiscardWounds，预期0不变；其余23例定义逐项相等。
- 仅重跑magic_021_13：`20260911_220957_5f6c50b2`通过。未重跑其他已通过用例，也未因配置字段修正重跑C#测试。
- 逐例引用独立运行证据合计24例已测检查通过（19 partial、5 passed）、495个有效效果断言；实际共95次按钮操作、49张截图（含补测与初始截图）。完整卡库未重跑。
- 最终Console两次0错误：`AutomationOutputs/Possess/20260911/console_final_1.json`、`console_final_2.json`。检查时间见各文件，约2026-09-11 22:11。
- 已检查混合攻击分次击杀与第二敌人跳过毒素后实际攻击两张代表截图；原卡面、费用、攻击/击杀/伤牌数值完整可读。所有本卡步骤textFits断言通过。
- 日常待测队列24→1→0。全库626定义保留；原606定义未改。首轮失败原报告保留，不回填伪造通过。
- 明确区分两个运行的配置SHA和逐例定义SHA：`AutomationOutputs/Possess/20260911/verification_summary.json`，没有把补测1例报告说成单次24例运行。
- 原始证据82文件/48,669,595字节留本地；提交精简摘要与索引，不上传到远端Git。

## 复现
1. `python tools/build_all_card_verification.py`。
2. `python tools/build_focused_card_verification.py --cards magic_021`（后续日常应优先选择改动/失败的具体case IDs；此次首轮另含magic_010_01、magic_011_28）。
3. Unity菜单 `Tools/Mage Knight/Original Cards/Run Focused Cases`。
4. 定向EditMode读取 `AutomationConfigs/editmode_focused.json`，菜单 `Tools/Mage Knight/Rules/Run Focused EditMode Tests`。
5. 用 `tools/summarize_all_card_verification.py --output <exact-run>` 核对该次实际运行；只有最新完整报告才可用 `build_focused_card_verification.py --failed-from <effects.json>` 剔除已通过用例。


## 尚未完成
- 魅惑的交涉奖励选色及一次招募折扣仍需后续联验；本批不把整张卡标为完成。
- 完整出牌区生命周期、全部原卡及其他组合仍未完成；既有partial结论保留。
- 远端上传继续暂停；证据本地保存，只提交必要文本与源代码，不上传截图。
