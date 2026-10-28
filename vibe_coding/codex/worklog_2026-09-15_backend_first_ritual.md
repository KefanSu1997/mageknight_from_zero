# 后台先行流程与仪式攻击

## 用户目标与策略

通用战斗demo暂缓，优先原卡全部验证。用户补充先快速验证真实后台状态，再进入场景。已写入AGENTS.md和本机MEMORY.md；已通过且无影响用例不重复运行。

新增tools/RulesCli：本地.NET 8直接编译Assets/Logic与同一份NUnit测试源码，不复制游戏规则，不启动Unity.exe。使用已有PackageCache的NUnit/Newtonsoft，NuGet来源及Targeting Pack下载关闭。必须显式筛选测试类、方法或参数化案例fullname；空/未知筛选失败，失败/跳过/不确定结果非零退出。输出独立NUnit XML、摘要和源码/原JSON指纹。详见tools/RulesCli/README.md。

第一次.NET恢复发现SDK试图要求本场景不用的桌面/Web引用包；保持离线，关闭EnableTargetingPackDownload后复用已有核心引用包。没有下载/更新Unity packages。

## 规则修改

对照中英文原卡advanced_card_029：去除另一张行动卡，按实际颜色决定攻击。原实现靠option猜颜色、丢失元素、把攻城当普通远程。RitualAttackEffect现在在付费前检查实际手牌引用、行动卡类型及原颜色；使用原CardColorHelper，按引用移除（不进弃牌），通过CombatPower添加正确的Physical/Fire/Ice/ColdFire和Melee/Ranged/Siege。错误option不能伪造材料颜色。

场景配置增加removeIndex/foreignRemove，展示实际目标ID、原颜色和引用身份。测试仍明确不覆盖完整出牌区生命周期（包括正在打出卡牌的身份与“另一张”限制的整合）；不因当前分支符合就判整张卡完成。

## 后台证据

- 20260915_224913_61f0a7d7：16项，14通过/2失败，0.766秒。失败是测试把蓝色“耐力”basic_card_014当绿色材料；原SO与JSON均为蓝色，未改动独立攻城数值预期，改选绿色“行进”basic_card_000。
- 20260915_225000_4c14057f：仅2个受影响参数案例，2/2，0.227秒。
- 20260915_225119_d69016ef：仅新增8项抗性/城防案例，8/8，0.313秒。
- 合计24项列明规则已符合：8种颜色/强化组合、实际击杀、同ID实例精确去除、非法卡种/外来实例、付费失败、元素抗性与单双城防。数字仅为测试执行耗时，首次编译另计。后续仅修改UI文本，没有再次运行后台用例。

后台独立目录：AutomationOutputs/RulesState。保留初次失败，不删除或伪造整批全绿记录。参数案例的SetUp在.NET与Unity都按项目工作目录加载原JSON。

## 场景证据

后台收敛后才运行16例（原2例+新增14例），没有全库执行。

- 首次20260915_225334_b60ca115/advanced_card：首个选择按钮因ui:textFits=False停止，无效果完成。新增目标信息把原2行夹具说明撑成3行；只将该卡夹具改为包含必要状态的2行，保留截断检查。失败截图与报告留档。
- 完成20260915_225801_97597f0d/advanced_card：16例、59次按钮操作（report含初始视图共60步）、335效果断言、205UI断言全部符合，14 partial/2 passed/0 failed。29张验收截图留本地，实际检查049_advanced_card_029_12_execute.png，原卡、绿色材料、攻城3、护甲2、击杀1、名望3和红魔力2→1均对应。
- 修改后DLL时间晚于源码；22:53:28/29、22:57:59及最终场景退出后分别取得Unity MCP Console连续两次0 error。
- --failed-from完成报告后日常focused队列为空，671例回归库与历史失败证据保留。

## 当前剩余

综合上一版657例账本与本批14新增，当前定义671例。最新混合历史统计165 passed、492 partial、14 failed（不是当前代码全量认证）。14失败中6个有效原卡效果问题、8个来自4个无有效牌面记录；仪式攻击历史元素失败已消除，但该卡完整生命周期仍待验证。当前没有Git提交/远端推送，没有上传测试截图或大报告。
