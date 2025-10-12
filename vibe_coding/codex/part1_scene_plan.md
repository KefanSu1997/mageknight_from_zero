# Part1 场景规划与进度记录

## 2025-09-16 规划

- **目标**：为 Part1 的核心系统搭建独立的 Unity 测试场景，覆盖核心循环、卡牌与资源、战斗、探索与招募，以及最小化流程整合。
- **约束**：
  - 不修改既有逻辑实现，保持 `hand_deck_test.unity` 可运行。
  - 每个场景聚焦单一功能块，并提供清晰的测试说明。
  - 复用现有的测试脚本（如 `Part1TestManager` 等）以减少重复逻辑。

### 计划中的场景

1. **Part1_DeckManaTest.unity**
   - 覆盖内容：牌库初始化、抽牌、手牌展示、魔力池增减。
   - 关键组件：`DeckRuntime`、`HandManager`、`Part1TestManager` 的卡牌与魔力测试按钮。
   - 预期交互：点击按钮后在 UI/Console 中输出牌库与魔力状态。
2. **Part1_CombatTest.unity**
   - 覆盖内容：`BattleResolver` 触发、简易战斗 UI 展示。
   - 关键组件：定制的敌我单位显示、`Part1TestManager` 的战斗测试按钮。
   - 预期交互：点击按钮后输出战斗结算日志。
3. **Part1_ExplorationRecruitmentTest.unity**
   - 覆盖内容：`MapState`、`ExplorationService`、`RecruitmentService` 的核心流程。
   - 关键组件：六边形地图占位物、招募面板占位 UI、对应按钮。
   - 预期交互：按钮触发探索/招募逻辑，日志显示结果。
4. **Part1_FullFlowTest.unity**
   - 覆盖内容：`RoundClock`、`TurnEngine`、卡牌抽取到回合结束的最小流程。
   - 关键组件：`Part1TestManager` 全部按钮、额外 UI 用于展示回合状态。
   - 预期交互：按顺序点击按钮模拟完整第一回合。

### 工作步骤

1. 场景与文件结构准备：创建 `Assets/Scenes/Part1/` 并复制基础 UI/摄像机框架。
2. UI 搭建：为 `Part1TestManager` 准备所需的 `Canvas`、`TMP` 文本与按钮，并根据不同场景裁剪按钮集合。
3. 功能钩子：在每个场景中布置对应逻辑对象（地图占位、战斗面板等），配置脚本引用。
4. 文档输出：在 `vibe_coding/codex/part1_scene_plan.md` 中持续添加进度与测试说明；最终在提交说明中罗列各场景用途与验证方式。

---

## 进度

- [x] 场景目录与基础 prefab 准备
- [x] Part1_DeckManaTest.unity 创建
- [x] Part1_CombatTest.unity 创建
- [x] Part1_ExplorationRecruitmentTest.unity 创建
- [x] Part1_FullFlowTest.unity 创建
- [x] 使用说明与验证手册整理

### 场景说明

- **Part1_DeckManaTest**：保留手牌与牌库系统，Harness 自动生成测试面板，仅启用牌库与魔力按钮。预期点击后在日志中看到抽牌与魔力数值。
- **Part1_CombatTest**：禁用牌库相关对象，仅启用战斗按钮，并自动生成战斗占位物。预期日志输出战斗流程检查。
- **Part1_ExplorationRecruitmentTest**：启用探索与招募按钮，自动生成地图、招募占位对象。预期日志打印探索/招募服务初始化结果。
- **Part1_FullFlowTest**：启用全部按钮并绑定所有系统，用于串联第一回合最小流程。预期日志按照按钮顺序输出流程节点。

## 2025-09-17 更新

- [x] 调整测试面板 UI：按钮改用垂直布局并扩大面板尺寸，避免与日志模块重叠。
- [x] 强化牌库测试：按钮触发重新洗牌并抽取 5 张手牌，同步输出抽牌详情。
- [x] 丰富魔力池测试：新增魔晶可视化区域，按钮随机增加各色魔晶并在日志中汇报增量与当前值。

## 2025-09-18 更新

- [x] 控制面板/日志改用独立列布局，去除共用背景并增加横向间距。
- [x] 日志与魔晶面板垂直排布，消除重叠并统一使用微软雅黑 SDF。
- [x] 优化手牌排布算法：基于 HandRoot 宽度居中并使用左侧锚点，抽牌后不再偏移出界。
- [x] 重构 Part1SceneHarness UI：测试按钮左侧分栏，战斗日志与魔晶概览位于右侧，视觉层级清晰。
- [x] 修复 HandManager 手牌排列：过滤失效子节点并重置锚点，重复抽牌后保持居中展示。
- [x] 本地化战斗日志：改写按键输出为中文，同时为各测试场景显式绑定微软雅黑 SDF 字体，消除方块字符。

## 2025-09-23 更新

- [x] 重构 Part1SceneHarness 布局：采用锚点定位的左侧按钮栏、右侧信息列与底部手牌区，贴合红/绿/黄/蓝四个功能区域规划。
- [x] 操作日志改为 ScrollRect + Scrollbar 固定高度实现，追加日志时由 Part1TestManager 自动滚动到底部，避免重要信息被遮挡。
- [x] 新增 HandArea 容器并自动绑定 HandManager.handRoot，手牌渲染保持在蓝色区域且随窗口缩放居中。
- [x] 魔晶显示面板调色并保留 ConfigureManaDisplay 管线，让 UI 与五色魔晶的对应关系更直观。
- [x] 修复滚动日志 Mask 与手牌重排问题：Viewport 使用 RectMask2D 避免遮挡，并在绑定新 HandRoot 时迁移现有卡牌保持初始布局一致。

## 待处理问题（2025-09-18）

- [ ] 暂无（待在高分辨率 / 缩放环境下进一步手动验证 UI 间距）。

## 2025-09-24 更新

- [x] 在 DeckManaTest 场景的 Info 列添加牌组/弃牌区面板，实时显示两者数量并提供查看入口。
- [x] Part1SceneHarness 动态生成 DeckZonesPanel，按钮点击后交给 Part1TestManager 展示内容。
- [x] Part1TestManager 记录弃牌和补牌流程，弃牌区顺序保持真实，牌组列表按名称排序隐藏真实顺序。
- [x] HandManager.ResetHand 改为将手牌推入弃牌堆再抽牌，DeckRuntime 增加状态事件以更新 UI。
- [x] 自动化按钮逻辑验证：测试牌库按钮会弃掉现有手牌，若牌组不足自动回收弃牌区。
- [x] 牌组/弃牌区点击后弹出卡图浮窗，原场景虚化为背景，支持网格浏览与空堆提示。

## 2025-09-25 更新

- [x] 调整牌组/弃牌卡图弹窗锚点，使其只覆盖信息列上方区域，避免遮挡日志与手牌。
- [x] 统一 DeckViewerOverlay 左侧偏移与控制面板宽度，保持关闭按钮和标题在红框范围内。
- [x] 通过常量化顶部间距与弹窗高度，方便后续根据分辨率快速微调。

## 2025-09-26 更新

- [x] 修复 Deck/Discard 预览弹窗遮罩后图片被遮挡问题，重建自适应网格并保持在红框区域内展示。
- [x] 自动化脚本验证牌组、弃牌浮窗及测试按钮流程，确认截图正常输出卡图。

## 2025-09-26 自动化输出

- [x] SceneAutomationOrchestrator 在创建截图目录时附加场景名与时间戳，防止覆盖旧流程。
- [x] DeckMana 自动化执行重新验证，最新截图位于 `AutomationOutputs/DeckManaTest/captures/<场景名>_<时间戳>/`。

## 2025-09-27 更新

- [x] 修正 DeckViewer 异步回调的日志代码，恢复 Part1TestManager 编译通过并保留加载/请求日志。
- [x] 梳理 DeckViewer 布局与列表生成方法，清理拼接残片确保栅格尺寸逻辑可编译。
- [x] 将 SceneAutomation 等待逻辑改为基于 Time.realtimeSinceStartup 的逐帧轮询，禁用 Pause on Play 并将 DeckMana 配置的等待时间压缩，避免初始延迟卡死。
- [x] 跟进自动化回归：DeckMana 场景脚本执行确认完成，报告成功。

## 2025-09-27 更新（晚间）

- [x] SceneAutomationStep 默认等待时间改为 -1，允许场景配置文件单独覆盖并避免旧逻辑强制 1 秒。

- [x] SceneAutomationOrchestrator 支持 initialDelay 与步骤等待，使用 WaitForSecondsRealtime 确保暂停逻辑与 TimeScale 无关。

- [x] DeckMana 自动化配置补充 0.3~0.6 秒等待，截图阶段摇摆稳定后再记录。

- [ ] DeckViewerOverlay 目前保持贴顶布局，自动化截图只截到标题栏，后续需调整锚点或截图区域以便捕获完整卡图网格。
  
  ## 2025-09-28 更新

- [x] 移除 DeckViewerOverlay ContentSizeFitter，改为在 Part1TestManager 中按列数和卡片数显式计算内容高度。

- [x] PopulateDeckViewerOverlayCards 在生成完成后强制刷新布局并重置 ScrollRect，确保初次打开与重新填充都能看到完整网格。

- [x] Scrollbar 改用 AutoHide 模式，避免在内容不足时压缩 Viewport 宽度导致卡图被遮挡。

- [x] 新增布局调试日志，卡片数为 0 时仍生成占位标签并参与高度计算。

- [x] 编译检查通过：通过 Unity MCP 控制台刷新后未发现编译错误。

## 2025-09-29 验证

- [x] 通过 Unity MCP 执行 `Assets/Refresh` 并抓取最新编译日志，确认 DeckViewer 重算高度逻辑无编译错误。
- [x] 运行 DeckMana 自动化流程（report.json status = success），截图包含完整卡图网格，日志显示内容高度 1872。
- [x] 更新 vibe_coding/codex 记录，补充自动化验证与经验笔记，方便后续回顾。

## 2025-09-30 更新

- [x] 抽象出 `ResizeGridContent`，在填充卡图后写回 `GridRoot.sizeDelta.y` 并立刻重建布局，ScrollRect 再次回到顶部即可看见完整网格。
- [x] DeckViewerOverlay 现在显式缓存 ScrollRect 与 Viewport 引用，避免在浮窗隐藏时 `rect.width` 变为 0 造成列数计算失真。
- [x] 自动化流程截图确认 `DeckOverlay` 内容高度约 1872，`LogDeckViewerContentState` 调试日志用于快速定位 childCount、sprite 赋值与 Pivot 配置。

## 2025-10-01 更新

- [x] DeckViewerOverlay 的 GridRoot 重新挂载 ContentSizeFitter，确保 ScrollRect 内容高度随卡片网格变化而增长，避免被 Viewport 遮挡。
- [x] 运行 DeckMana 自动化流程复核截图（报告：AutomationOutputs/DeckManaTest/report.json，status=success），确认截图恢复显示卡图。

## 2025-10-02 更新

- [x] DeckMana 自动化回归通过，按钮路径依旧使用 Canvas 根节点，SmokeTest 生成的方块在 Populate 之前会被覆盖。
- [x] 为 Part1SceneHarness 添加 Editor-only 的 SmokeTest，直接向 DeckViewer GridRoot 注入 UI Image 方块，用于快速判断 ScrollView 框架是否渲染正常。
- [x] CreateCanvas 改为始终新建 ScreenSpaceOverlay Canvas，排序提升至 5000，避免复用场景中的 World Space/Camera Canvas 导致遮挡。
- [x] Part1TestManager 引入 ForceDeckViewerLayoutImmediate，填充卡图后立刻执行 Canvas.ForceUpdate + LayoutRebuilder，紧接日志输出与 ScrollRect 复位。
- [x] 调整 DeckViewer Debug 日志，输出 child 数、RectTransform size/sizeDelta 与首个 Image 的 sprite，以便对照烟雾测试结果定位资源问题。

## 2025-10-09 更新

- [x] 扩大 DeckZonesPanel 内卡牌区与弃牌区按钮高度（140px）并提高预览正文最小高度，保证卡图浮窗纵向空间。
- [x] DeckViewerOverlay 改为 40px 内边距 + 720px 高度，GridLayout cell 调整为 320x480、纵向间距 24px，便于展示更大卡图。
- [x] Part1TestManager 调整 DeckViewer 默认列数为 3、卡宽 320px、最小宽 200px，匹配新的浮窗宽度。
- [x] 运行 Tools/Scene Automation/Run DeckMana Automation，报告 success，截图 20251009_223321 显示卡图区域明显增大。

## 2025-10-10 调整

- [x] 重新划分 DeckViewerOverlay：标题栏固定 64px，高亮背景与正文分离，正文四周 24px 内边距，Subtitle 贴紧上沿。
- [x] GridLayout spacing 调整为 (24,32)，卡片 cellSize 扩展至 340x510；Part1TestManager 将列数上限提升到 6，并把卡宽约束改为 320~340，让 Viewport 自动塞满可见列数。
- [x] 自动化回归（captures/Part1_DeckManaTest_20251010_001231）截图与用户蓝/红框期望一致，日志输出 cellSize=340x510。

## 2025-10-16 更新

- [x] 为 Part1_ExplorationRecruitmentTest 增加探索与招募专用面板：地图六边形实时着色，摘要与提示随按钮刷新。
- [x] 实装探索服务示例逻辑：维护探索队列，依次演示六邻格探索并同步剩余移动力。
- [x] 整合招募流程示例：影响力判定、候选/已招募列表动态渲染，地块变化时播报可招募单位。
- [x] 新增 AutomationConfigs/part1_exploration_recruitment.json 自动化脚本，覆盖两次探索 + 一次招募的截图流程。

## 2025-10-12 Part1_CombatTest 玩家视角梳理

- [x] 进入场景后即可看到“战斗演示”信息列，左侧保留单一“测试战斗系统”按钮，不混入牌库/魔力按钮。

- [x] 信息列需提供英雄与敌方两个独立的卡片板块：显示名称、等级、护甲、攻击骰面以及关键能力标签，颜色区分阵营。

- [x] 面板顶部放置场景标题与“切换对阵”按钮，允许快速轮换至少 3 组预设战斗组合。

- [x] 面板底部应有战斗结果摘要（胜利/失败、创伤、名望），并提示 BattleResolver 流程是否执行完整。

- [x] Logs ScrollView 延续 DeckMana 风格，点击按钮会同步更新板块与日志，玩家无需打开 Console 即可理解结果。

- [x] 自动化测试需要：初始截图、点击“测试战斗系统”后截图/日志验证，以及轮换到下一组对阵确认 UI 文本更新。

- 本次实现：Part1SceneHarness 动态生成 CombatPanel（标题、切换按钮、英雄/敌军信息卡、结果栏），Part1TestManager 增补三组示例战斗并驱动 BattleResolver，新增 Scene Automation 配置 `AutomationConfigs/part1_combat.json` 及菜单入口，供自动化切换对阵与回归测试。
