# 2026-10-11至12 冻结预训练文本编码器接入 UI PPO

用户选择冻结文本编码器，只训练动作头，将原卡文字、状态与候选描述编码后接现有结构化UI PPO。

## 实施

独立.venv_ui_text安装CPU PyTorch/Transformers，Unity packages、C#规则、原任务池与既有模型档案均不改。实际下载jhu-clsp/mmBERT-small固定revision abc32620dd4f6ab06f5fbe905dc25f310618e09f，模型与tokenizer文件SHA入合同；140,493,696编码器参数，384输出。只载编码器，4个预训练MLM输出头参数明确不用；缺失/不匹配编码器权重拒绝。新Transformers不接受旧reference_compile构造关键字，首probe失败后移除，未改权重或退回哈希/随机编码器。

编码器eval、requires_grad=False、inference_mode，仅CPU前向。最多256token分块，每块均进入mask加权mean pooling，合并并L2归一化；文字缓存按模型签名/原文绑定。原卡基础/强化费用/效果按原JSON文字，不猜规则；候选读取控件显示和公开说明，state沿公开白名单，不将rl.sharedRuleState、隐藏牌序或教师动作喂给actor。PyTorch冻结encoder与NumPy优化器完全分离，没有LoRA或encoder优化器。

global96和candidate64各追加384维，得到480/448维；PPO actor/critic共90,369训练参数。旧参数/Adam行全保留，新文字行/动量零初始化，旧RNG保留。实际旧UI checkpoint_002的2更新是父历史；本次新两次更新编号3、4，不当4次文字训练。

## 门禁与真实结果

- 六个真实UI观察，暖迁移完整候选概率/value最大差0；隐藏审计字段变造不改变任何输入；全部21参数/Adam数组和RNG恢复精确。错误encoder版本拒绝。新增文字行数值梯度最大差1.8345512677068826e-11。
- train_live：28步与29步，共57真实策略点击，2新PPO更新/8新Adam步。两次7参数数组变化，49,152新增文字actor权重非零；两个原生End均won=False。
- encoder完整state_dict SHA前后均2099340b820b0ad917fa5a93dec764188123e2a691648024e708acd6bf8ce42c，0可训练encoder参数/0梯度张量。不是靠requires_grad声明代替实际权重校验。
- 首frozen_live 4步打开原魔力汲取输入面板，旧SCOPE遗漏DeedInputs的实际按钮，停止时Ranged/won=False。曾在commentary误说进入战斗收尾，已即时纠正；旧轨迹/摘要未改。
- 显式DeedInputs scope扩展仅加入实际可点击的基础/强化选择、确认、返回。--change-ui-scope和父/新合同记录，不热改训练/不放宽规则。frozen_deed_inputs 29步到原生End，仍won=False；原始抽牌不同，而且该局没有操作DeedInputs，不能当缺口已实际回归或能力改善。两个冻结run的全部21参数/Adam数组保持训练末值。
- 另用原实际UI脚本选当次起手有的basic_card_009并打开面板，单列条件fixture。冻结policy实际2点击Btn_InputChoice_base/Btn_ConfirmInputs并退出面板；非教师训练数据，不作自主整战胜利。概率/value/RNG重建差0，108共享字段对照通过。
- 主90点击完整对应/4860共享字段前后比较通过，2条件面板点击另列；首次配置5点击和面板入场3点击均不计策略训练。
- 最终Console实际两读success=true、data=[]，console_complete_1/2.json。旧PPO数值/恢复/公开隔离三项回归通过，新输入门禁在scope修复后再次通过。

## 文件

- tools/BattleRl/frozen_ui_text.py：固定模型、冻结、完整文本分块与签名缓存。
- tools/BattleRl/train_frozen_text_ui_ppo.py：暖扩展、PPO、实际点击、签名恢复和显式scope迁移。
- tools/BattleRl/verify_frozen_text_entry.py、verify_frozen_text_deed_panel.py：真实输入门禁与条件面板验证。
- tools/BattleRl/FROZEN_TEXT_UI_PPO.md：运行说明。
- AutomationOutputs/StructuredUiPpo/20261011_frozen_text/summary.json、action_text_click_mapping.csv：主运行索引；原文/vector SHA与实际点击关系保留。
- 同目录train_live/frozen_live/frozen_deed_inputs、deed_panel_actual保留原始状态/概率/截图/检查点；encoder缓存为AutomationOutputs/StructuredUiPpo/frozen_text_cache，预训练权重在本地HF缓存，文件SHA记录在checkpoint。

## 未完成的能力

这证明真实预训练文字已进入可恢复的PPO动作头，并且encoder保持冻结；并非Jev/Laya或RLCD算法复现。没有获胜或流畅度改善结论，实际规则理解须另验证。当前仍Tovak单战试玩，无GRU、完整单位/技能、复杂费用/分页、连续多战跨轮及未见构筑评价。候选softmax是策略选择概率，不是已校准成功率。CPU运行没有GPU训练/显存数据，也不能据此外推全原范围训练预算。
