# 冻结 mmBERT 文字编码器 + 结构化 UI PPO

本入口复用 structured_ui_ppo.py 的候选控件、实际 Demo 配置前缀、EventSystem 点击及 PPO 更新。新文字读入在 frozen_ui_text.py，新训练入口在 train_frozen_text_ui_ppo.py；旧模型、档案、C#规则和界面不改。

## 编码与冻结

- 模型：jhu-clsp/mmBERT-small，固定 revision abc32620dd4f6ab06f5fbe905dc25f310618e09f；实际载入编码器140,493,696参数，384维输出。
- AutoModel只读本地下载文件，不运行远程代码；缺编码器权重或形状不一致拒绝加载。预训练MLM生成头的4个参数明确不使用。
- 参数requires_grad=False、eval、torch.inference_mode；训练优化器仅含NumPy actor/critic的参数，没有文字模型。
- 原卡基础、强化费用、强化效果/法术双面/神器普通焚毁按既有原JSON文字组成描述；候选控件加实际显示文字与当前说明。state只读取公开白名单与当前候选控件，不读rl.sharedRuleState、隐藏牌序或教师动作。
- 文字分成最多256 token的块，每块均编码；按attention mask合并所有块再L2归一化，不静默丢弃后半段。固定规则原文与运行时文字均可缓存。
- 缓存键包含模型文件SHA、版本、pooling、dtype与原文；checkpoint合同包含编码器签名和5原文本JSON SHA，错误版本拒绝恢复。每步保存实际原文和向量SHA。

## 暖迁移和更新

保留旧global96/candidate64特征，分别追加384文字维度，得到global480/candidate448。64隐藏层的actor/critic共90,369可训练参数。新文字输入行与Adam动量零初始化，旧所有行、其他参数、动量与actor RNG原样保留；因此入口概率保持旧策略，后续真实PPO再学习如何使用文字。

本次从真实UI试验 checkpoint_002 恢复，旧2次更新保留，新两次更新计数为3、4，不能说全部4次都是文字训练。输入门禁用6真实UI观察核完整候选概率/value最大差0、隐藏字段隔离、全参数/Adam/RNG恢复及错误encoder版本拒绝；新增文字行数值梯度最大差1.8345512677068826e-11。

输出：AutomationOutputs/StructuredUiPpo/20261011_frozen_text。训练2局57真实策略点击，两个正式End均won=False；两次7参数数组改变，49,152新增文字actor权重非零。文字编码器state_dict完整SHA前后相等，0可训练参数、0梯度张量。该证据证明真输入/更新，不证明卡牌规则理解或能力提升。

保存后第一次冻结4点击，在Ranged打开魔力汲取DeedInputs，但旧候选范围没有面板按钮，按无支持候选停止。旧失败不覆盖。新增显式--deed-inputs/--change-ui-scope仅追加实际面板的基础/强化选择、确认与返回；restore必须显式承认scope变化，21参数/Adam数组和RNG保持。扩展后另一次冻结29点击到End仍未胜，因初始抽牌不同，不当配对改善；该局未实际点击DeedInputs。

因此另做条件面板实例：独立脚本经原入口选择起手已有的魔力汲取，打开真实面板（不算PPO样本），冻结策略自主选择Btn_InputChoice_base与Btn_ConfirmInputs，2实际点击离开面板。完整分布/value/RNG与108共享字段比较通过。该脚本前缀和条件子集不是未见构筑评价。

主运行90实际策略点击（57训练、4旧冻结停止、29扩展冻结）；完整候选概率/value重建差0，4860共享字段前后对照通过，2条件面板点击另列。训练与冻结均未胜。最后Console两读零错误。action_text_click_mapping.csv保存原文、向量SHA、动作概率、路径、坐标、实际命中及前后阶段。

## 运行

独立Python环境.venv_ui_text，不改Unity packages或全局Python；首次下载CPU PyTorch、Transformers与固定模型。实际环境版本保存runtime_requirements.txt。首次兼容问题为新版ModernBertModel不接受旧reference_compile关键字；已移除，不改模型权重。

```powershell
.venv_ui_text/Scripts/python.exe tools/BattleRl/verify_frozen_text_entry.py --out AutomationOutputs/StructuredUiPpo/new_text_entry
.venv_ui_text/Scripts/python.exe tools/BattleRl/train_frozen_text_ui_ppo.py --out AutomationOutputs/StructuredUiPpo/new_text_train --legacy-checkpoint AutomationOutputs/StructuredUiPpo/20261011/train_verified/checkpoint_002 --updates 2 --horizon 32 --local-only
.venv_ui_text/Scripts/python.exe tools/BattleRl/train_frozen_text_ui_ppo.py --out AutomationOutputs/StructuredUiPpo/new_text_frozen --checkpoint AutomationOutputs/StructuredUiPpo/new_text_train/checkpoint_002 --updates 1 --horizon 32 --frozen --local-only --deed-inputs --change-ui-scope
```

Unity项目和已有MCP桥须打开，只能一个实例操作Unity。输出目录必须全新；关闭后复制模型、轨迹与文字缓存时保持模型签名一致。

本次reader与PPO头均在CPU运行，未使用GPU/LoRA，也不是官方Jev、Laya或RLCD训练配方的复现。预训练多语言表示有可能改善文字使用，但须正式对照验证；现范围仍只是Tovak单战试玩。单步候选softmax不是成功率或已校准置信度。单位/技能/复杂付款/分页、多战跨轮、全原范围与真正泛化仍欠。
