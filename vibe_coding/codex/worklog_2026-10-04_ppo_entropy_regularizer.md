# PPO探索熵的类型参数数量偏置

## 发现与修改

保存v40-4096策略实际seed100000在AssignDamage重复01/10承伤排序；此处unitDamage有许多参数，而advance只有一项。原联合熵为H(kind)+ΣP(kind)H(tuple|kind)，使类型头在参数较多的类型上得到额外探索奖励，即使同样是规划、不产生实际战果。不能断言这是循环唯一原因，但数学上存在数量偏置。将分层策略熵改成H(kind)+对当前可用类型的条件熵取平均，类型头只受类型熵和原PPO似然影响；条件头仍保持探索。PPO clipped likelihood/GAE、原规则、合法动作及奖励不变。平面/类型均衡旧策略分支保持原联合熵。

同时run_manifest增加entropy_regularizer明确新轮训练配置。现活v40已加载旧Python函数，仍运行冻结v39DLL；不把代码修改冒充活轮已切换，也不停止/重复启动活进程。

## 验证与缺口

flat/balanced/hierarchical数值梯度最大误差约1.35e-11/1.56e-11/9.91e-12。2/32个规划参数与1个提交参数，均匀类型头的纯熵梯度新方案为0；旧方案分别-0.00173/-0.00866偏向更多参数类型。新方案纯熵更新后提交概率均保持0.5。`AutomationOutputs/BattleRl/20261004_hierarchical_entropy_check.json`。Unity Console两次0错误 `tmp/rl_entropy_math_console_20261004.json`；仅修改Python训练代码，不修改场景。

实际v40已6144步114局，两家族/7英雄/69敌实际开战记录，不代表全原卡或全部模式验收。新WisdomBook seed45935物理材料20、本体22去除，原供应取得蓝法术016物理81与蓝晶，后续同81实际蓝费1施放的8条断言通过；空间扭曲实际移动/跳格/肆虐消费者本轨迹缺失，不能记为该原牌完整通过。`AutomationOutputs/BattleRl/20261004_v40_wisdom_acquire_cast_review.json`。下一轮是否提高胜率必须实际训练/评估，数学通过不证明可用模型。
