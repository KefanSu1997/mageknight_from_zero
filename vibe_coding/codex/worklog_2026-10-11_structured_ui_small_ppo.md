# 2026-10-11 结构化 UI + 小型 PPO 实际 rollout

用户要求尝试结构化 UI 状态与小型 PPO，建立操作序列与实际 Unity Demo 点击过程的对应，并提供实例验证。

## 实际结果

新增独立 UI 策略，不修改既有原战斗模型、规则、任务池或训练历史。16,641 参数、NumPy CPU 前馈 actor/critic；没有视觉模型、computer-use 模型或 GPU 训练。它读取实际控件的名称、显示文字、位置和可点击性，加上公开状态白名单。rl.sharedRuleState 只用于审计，不进入 actor。当前无 GRU。

Unity Hub 打开 6000.6.0f1，复用已缓存 Unity MCP server 和已安装 package；没有包变动或 batchmode。新增菜单打开 Part1_PveBattle 实际场景，1920x1080 Game View。Python 与运行期执行器逐次交换观察/单控件动作，EventSystem 射线核实际命中后发送 down/up/click。执行器不代模型补步骤。英雄选择、战术、开始回合与正式开战为五个实际脚本前缀，单独标为 scripted_setup_not_policy。

| 运行 | 实际策略点击 | 结果 | 更新 |
|---|---:|---|---:|
| train_verified/episode_000 | 21 | 正式 End，won=False | 1 |
| train_verified/episode_001 | 32 | step limit，Melee，won=False；按实际 value bootstrap | 1 |
| frozen_live/episode_000 | 28 | 正式 End，won=False | 0 |

训练共53样本、2 PPO更新、8 Adam步；两次均7参数数组实际变化。每次保存7参数+14动量数组和 actor RNG。冻结模型加载后再操作28步，全部21数组与训练最后检查点逐项完全相等，更新计数仍2。81自主点击不是81次训练使用，也不代表模型学会获胜。

从对应保存模型重算每步完整候选分布、value及 RNG 选择：最大概率差0、全部实际选项一致。按钮路径、实际命中、1920x1080坐标、前后观察版本核验通过。81步的27共享字段前后共4374比较零差异，仅证明显示/规则快照映射。

## 独立实际实例

18脚本点击、19精确断言通过，另972共享字段比较通过。脚本与模型轨迹分开，不作为教师训练样本：

1. 正式 Ranged→Block；选牌后 hand保持5，横置后5→4、block0→1、played0→1。
2. 点击实际“重置本阶段”，hand.ids精确恢复原顺序、block回0、played回0。这是实际 Demo 检查点入口，不是重建玩家快照。
3. Block→AssignDamage→Melee，实际提交英雄承伤；三张实际非伤卡各横置，近战贡献0→1→2→3。
4. 点击实际敌人、打开贡献选择、全部、完成、结算；kills0→1、melee3→0、attacks.sources为空。
5. 另点下一阶段正式End，won=True；击败敌人和正式整战收尾分别断言。该单战胜利来自验证脚本，不是 PPO 能力。

三项真实负例也通过：开局英雄面板遮挡手牌、陈旧观察版本、不存在控件路径。拒绝发生在指针事件前，前后实际观察版本完全相等。

## 失败、修复及数值门禁

- 最初 File.Move 三参数在当前 Unity 编译配置不可用，Console 保存 CS1501，改为支持的文件接口。
- 背景 Game View 暂停使观察超时：仅在显式 pilot 中启用 runInBackground，菜单激活 Game View。第一失败无训练更新，保留 train_smoke。
- 实际初始界面是英雄选择面板，手牌虽存在却被遮挡。保留真实mask，不取消遮挡约束；增加通过实际原入口的独立配置前缀。train_smoke_background零样本不算PPO训练。
- Windows原子替换与Unity读文件有短暂共享锁。Python有界重试，Unity下帧重读；响应按请求ID独立保存，禁止给旧ID写新的读失败。train_original和train_live失败序列保留，没有恢复未保存权重或追加为正式训练样本。
- Unity JsonUtility把null click字段序列化成空对象，首负例的null断言失败保留negative_examples。修验证器为检查无buttonPath/hitObject/success，negative_verified三负例通过。
- PPO解析梯度包含正负优势、上下裁剪、entropy/value，共84个数值坐标，最大差1.216390626106043e-10。检查点参数/动量/RNG精确恢复和实际观察隐藏字段隔离测试通过。
- 最终实际Unity Console两读均success=true、data=[]，见console_final_1.json和console_final_2.json。

## 查看证据

目录：`AutomationOutputs/StructuredUiPpo/20261011/`。

- `policy_click_mapping.csv`：81行自主操作，含按钮、坐标、命中、概率、阶段、手牌、贡献、拒绝、截图路径。
- `run_summary.json`：训练、冻结、实例及对照分母。
- `train_verified/episode_*/trajectory.json`、`frozen_live/episode_000/trajectory.json`：完整候选概率与实际前后快照。
- `*/exchanges`、`*/responses`：逐请求记录，失败不删除。
- `independent_examples/examples.json`与`verification.json`：18脚本点击及19断言。
- `negative_verified/negative_verification.json`：三真实拒绝例。
- `source_manifest.json`：当前实际编译DLL/代码SHA，事后采集不冒充历史预绑定。

关键截图：

![横置后实际格挡1](../../AutomationOutputs/StructuredUiPpo/20261011/independent_examples/captures/0009_b97d59f0.png)

![三份贡献消费后正式战斗胜利](../../AutomationOutputs/StructuredUiPpo/20261011/independent_examples/captures/0024_52e8bec9.png)

## 保持的边界

本次证明rollout、PPO更新、保存加载与实际UI点击对应能工作。训练/冻结三局均未胜，不作策略晋升或操作流畅度改善结论。奖励只是UI进展代理，仍会重复选择或无效结算；没有修改原环境reward和规则。0.15秒人为稳定等待包含在elapsedSeconds中，不当真实输入反馈延迟。

当前只验证Tovak一场原牌战斗试玩，非完整地图标准开局。其他英雄、费用材料、单位技能、复杂分页、多战/跨轮、全部原要素与未见组合均保持缺口。控件文字直接输入也不证明真人能理解界面。完整原游戏RNG/隐藏牌库尚未保存，不能声称重新启动会逐状态重放原Unity会话；离线重建概率与RNG选择和当前实例实际点击是不同证据。

可复用入口与命令见 tools/BattleRl/STRUCTURED_UI_PILOT.md。

## 用户要求长期保存映射（本次跟进）

已封存至AutomationOutputs/StructuredUiPpo/archives/20261011_action_click_v1_verified目录及ZIP。99行action_click_index.jsonl按role区分81策略与18脚本操作，保留每步局部候选index、控件身份、实际坐标/命中、观察版本、概率及原始证据指针；原始轨迹仍含全部概率与完整前后观察。模型、Adam/RNG、截图与源码/实际DLL也保留。375文件SHA和ZIP完整性逐项验证通过，7份源码/DLL均与原manifest签名一致。ZIP SHA256=63982bd4e1c315677519e8d4b71a313f402f6f0f93f7ac18a545f964ce4855df。

首份档案检验时Windows反斜杠与ZIP标准成员斜杠不一致导致KeyError，保留该档；新verified档改为POSIX相对成员路径，通过完整读回校验。没有改变模型或原轨迹，没有新增训练/Unity代码。控件坐标是历史结果，复用要重新解析实际控件与版本，不能当固定屏幕动作空间。

当前state特征与后续v2建议已补到STRUCTURED_UI_PILOT.md。v1字符哈希不具语言理解，缺完整实体、付款义务与记忆；v2仅为设计说明，本次未改变已训练输入或继承能力。
