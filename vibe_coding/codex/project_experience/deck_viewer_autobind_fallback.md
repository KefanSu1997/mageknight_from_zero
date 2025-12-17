# DeckViewer 自动补绑：在未使用 SceneHarness 时恢复浮窗

## 背景
- 部分运行时场景直接引用早期 Deck UI 资产，未经过 `Part1SceneHarness` 的程序化构建。
- 这些场景中的 `Part1TestManager` Inspector 字段保持空值，导致牌组按钮没有注册 `OnClick`，浮窗根结点也未配置关闭按钮。

## 处理步骤
1. 在 `Part1TestManager.SetupUI()` 末尾调用新方法 `EnsureDeckViewerFallbackBindings()`。
2. 该方法通过 `GameObject.Find(\"Canvas/Part1TableRoot\")` 定位 UI 根，再按固定路径抓取：
   - `DeckStack`、`DiscardStack` 按钮，以及对应的 `Count`、`ClickFlash` 组件；
   - `DeckSummaryPanel/Header`、`Body` 标签；
   - `OverlayRoot/DeckViewerOverlay` 下的背景按钮、关闭按钮、标题、副标题、ScrollView Grid。
3. 找到组件后复用 `ConfigureDeckZoneUI` 与 `ConfigureDeckViewerOverlay` 现有流程，确保事件订阅与地址刷新一致；同时在补绑完成后主动关闭 Deck 概览面板的 Header / Body 节点，避免旧版提示遮挡浮窗。
4. 若任一关键节点缺失，打印中文警告方便后续排查，但不阻塞其余模块运行。

## 收获
- 保证老场景（或外部团队手动搭建的 UI）只要命名遵循 `DeckUiTableBuilder` 约定，即可自动恢复浮窗交互，无需重新布署 SceneHarness。
- 将兜底方式封装在 Manager 内，避免重复维护 Inspector 绑定，也让自动化脚本继续沿用既有路径，且默认隐藏旧的概览块，画面保持干净。
- 统一在同一入口执行 Addressables 释放与布局刷新，减少“浮窗无内容”与“按钮无响应”的差异化调试成本。
