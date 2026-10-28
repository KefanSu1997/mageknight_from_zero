# 结构化 UI + 小型 PPO 实际 Demo 试验

此入口训练独立的 UI 策略，不修改原战斗 PPO 的模型、规则、任务池或历史。首次范围为 Tovak 原 16 牌战斗试玩：通过实际界面选择英雄、战术、开始回合和开战，然后由模型逐次选择当前控件。配置前缀单列为 scripted_setup_not_policy。

模型是 16,641 参数的前馈网络：96 维全局特征、64 维候选控件特征、64 维隐藏层。输入来自真实控件的名称、显示文字、位置、可点击性和明确白名单中的公开状态；不读取截图、未来牌序或 rl.sharedRuleState。后者仅作为审计证据保存。当前无 GRU，也没有预训练语言模型。后端为 NumPy CPU float64；本次未使用 GPU。

候选掩码只检查实际控件的 active/interactable、屏内中心以及 EventSystem 射线首命中的点击处理对象，不调用规则合法性预检。规则拒绝仍在轨迹中可见。控制范围显式排除配置、重置及战后入口；这是一场战斗的初步 UI rollout，不是全部 Demo、多战、跨昼夜或全原要素完成。

每次决策通过文件协议执行一个控件点击，发送 pointerDown/pointerUp/pointerClick，等待实际界面刷新，再采集控件与前后状态。执行器不自动补付款、目标或下一阶段。屏幕坐标、hitObject、按钮路径、观察版本、完整候选概率、所选动作和 RNG 保存，形成一对一证据。观察版本不一致或按钮被遮挡时拒绝执行。当前等待含人为 0.15 秒稳定窗口，elapsedSeconds 不能作为游戏输入响应延迟或真人思考时间。

PPO 参数：clip=.2，gamma=.99，GAE lambda=.95，entropy=.01，value loss=.5*(V-return)^2，学习率 .001，4 epochs/update，梯度范数上限 .5。奖励是独立 UI 进展代理：每步 -.02、实际拒绝 -.2、实际出牌 +.5、击败 +1、阶段变化 +.1。没有改原规则，也没有把该代理当获胜目标。step limit 使用 bootstrap，不伪造终局。修改这些目标需要新实验。

## 运行

先通过 Unity Hub 打开项目。使用已安装的 Unity MCP 桥接；不安装包、不调用 Editor batchmode。启动桥后可用 local_unity_mcp.py 调用已有工具。Start Structured UI Pilot 菜单仅在明确请求时打开真实 Part1_PveBattle 场景、1920x1080 Game View，并安装运行期执行器。

```powershell
python tools/BattleRl/structured_ui_ppo.py --out AutomationOutputs/StructuredUiPpo/new_train --updates 2 --horizon 32
python tools/BattleRl/structured_ui_ppo.py --out AutomationOutputs/StructuredUiPpo/new_frozen --checkpoint AutomationOutputs/StructuredUiPpo/new_train/checkpoint_002 --frozen --updates 1 --horizon 32
python tools/BattleRl/verify_structured_ui_pilot.py --training AutomationOutputs/StructuredUiPpo/new_train --examples AutomationOutputs/StructuredUiPpo/new_examples --negative AutomationOutputs/StructuredUiPpo/new_negative
python -m unittest discover -s tools/BattleRl -p test_structured_ui_ppo.py -v
```

输出目录必须是新的；只有一个实例能操作 Unity。metadata.json 保存参数规模、schema、源码 SHA、更新计数、Adam 步与 RNG；model.npz 保存7参数数组及14动量数组。trajectory.json 每个样本保存实际控件分布和点击前后数据；exchanges 和 responses 保留全部请求，包括失败。

2026-10-11 证据位于 AutomationOutputs/StructuredUiPpo/20261011。两训练局53点击、2更新；保存后冻结28点击、0更新；三次数值/公开输入/恢复测试通过。训练和冻结均未获胜。18个独立脚本点击取得单战胜利，只用于 UI 映射与消费者验证。详情见仓库工作日志。

## action 到实际点击的封存档案

用户要求后续复用，因此已另存 `AutomationOutputs/StructuredUiPpo/archives/20261011_action_click_v1_verified.zip`，解压目录同名。375文件逐项SHA256及ZIP完整性已检验，7份源文件/实际DLL与原记录签名均一致。包含81策略点击、18脚本点击、3拒绝例、模型/Adam/RNG、原始观察和实际截图。`action_click_index.jsonl`为99行索引，role保留策略训练、冻结与脚本的区别；manifest.json包含版本、特征合同、文件SHA和绑定边界。最初ZIP成员校验用了Windows反斜杠，读索引失败；原档保留，新verified档改用POSIX成员路径并完整通过。

动作index只在该步候选列表中有意义，不能作为固定全局动作ID。实际控制身份由该步buttonPath及控件记录定位；坐标是当时的实际结果，不是后续版本硬编码的点击位置。复用时应重新观测控件、检验版本与raycast，再执行。源码快照与原完整观察保留，方便未来修改state编码后重新解析；旧奖励与旧特征schema不得静默覆盖。

封存命令：`python tools/BattleRl/archive_structured_ui_mapping.py --source AutomationOutputs/StructuredUiPpo/20261011 --destination <新的绝对或项目相对目录>`。既有档案禁止覆盖。

## state space 后续设计说明（尚未实现v2）

当前v1全局96维：6阶段one-hot、14个公开数值tanh(x/10)、12维公开说明字符哈希、64维候选平均。每候选64维：16类别one-hot、6位置/大小/选择标记、32字符哈希、其余预留。actor输入为global+candidate共160维，共享MLP为每个候选打分并softmax。文字哈希没有预训练语义，也丢失词序；候选平均不能保全全部实体关系。v1只证明接口和真实更新。

计划v2把输入显式分为：公开游戏全局量（阶段、昼夜、资源、次数、当前义务）；变长公开实体（手牌/模式/敌人/部队/技能）；实际UI状态（面板、分页、所选物理实体/模式/目标/付款与提交条件）；候选动作及其实体链接；最近动作/拒绝/状态变化与小GRU。费用、效果、攻击类别、抗性等语义应从既有原定义与正式公开派生结果编码为数值/枚举，不仅用原ID或字符串哈希，更不伪造执行结果。不同同名实体分槽绑定，隐藏牌序不输入。

小模型学的是这些公开特征与真实后续结果的关联：例如选牌后才出现模式，选择强化后产生付款义务，提交才消费资源。结构化编码负责表达含义，PPO学习选择与顺序；奖励和完整终局任务仍决定学到什么。该v2提案不能当已接入或模型已理解原规则，需按新schema与独立迁移/输入门禁实施。

## 验证边界

- 验证器从每局对应保存模型重算完整候选概率、价值和 RNG 选择，再核按钮路径与真实命中，比较显示字段与同一实际规则快照。这是映射一致性，不能独立证明全部规则正确。
- 验证脚本对横置、恢复、三份攻击贡献消费与正式 End/won 另做精确断言；脚本操作不作为教师训练样本或自主获胜。
- frozen 模式只验证加载保存策略后的真实操作与参数冻结，不是独立未见构筑能力评估。
- 当前记录无法恢复完整 Unity 原会话 RNG/隐藏牌库。概率与操作记录可重建，不宣称再次启动会生成完全相同的原游戏轨迹。
- 文字可读性、布局理解与人类体验仍需人工验证；付款、单位、技能、分页与后续轮次尚欠实例。模型没有达到流畅玩家能力。
