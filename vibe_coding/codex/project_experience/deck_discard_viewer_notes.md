# Deck / Discard Viewer 联动记录

## 背景
- DeckTest 场景需要可见的牌组与弃牌区，点击即可检视当前卡牌。
- 要求牌组列表不得暴露真实抽牌顺序，但弃牌区需保留堆栈顺序。
- 现有 UI 由 Part1SceneHarness 动态生成，需要在不破坏自动化按钮路径的前提下扩展。

## 实施要点
1. 在 Part1SceneHarness 中插入 `DeckZonesPanel`，按钮与 ScrollRect 完全代码构建，避免额外 prefab 依赖。
2. 通过 Part1TestManager 新增 `ConfigureDeckZoneUI`，集中处理按钮事件、计数刷新与内容展示。
3. DeckRuntime 增加 `DeckStateChanged` 事件，Draw/Discard/Shuffle 全链路触发，UI 同步不再依赖轮询。
4. HandManager.ResetHand 改写为先弃置后抽牌，确保牌组不足时能自动回收弃牌堆。

## 经验
- 牌组预览如果需要隐藏顺序，可对 `drawPile` 使用 `Select -> OrderBy`，同时在标题上标注“按名称排序”提醒使用者。
- Discard 使用 `Stack.ToArray()` 即可获取“顶 -> 底”顺序，无需额外栈操作。
- ScrollRect 作为常驻面板时，要留出 Sidebar 宽度再配置 `RectMask2D`，否则滚动条会遮挡文本。
- 弹出卡图浮窗建议使用独立 CanvasGroup + 背景 Button，既能拦截点击也方便统一关闭，同时保持内容在编辑/运行模式下可复用。
- 加载卡图时独立请求 Addressables 句柄，封装在弹窗关闭流程中统一 Release，避免干扰手牌等其它监听者。
- 事件驱动的 UI 更新比按钮回调里手动刷新更稳健，避免遗漏例如自动摸牌、测试脚本等入口。

## 教训
- `handManager.ClearHand()` 如果只是 Destroy GameObject 而不通知 DeckRuntime，会造成弃牌区数量与实际不符。
- 仅在 OnClick 内执行刷新会漏掉初始化与自动流程，必须在 UI 绑定时立即调用同步函数。
- DeckRuntime.Shuffle 在空列表上早退时也要触发事件，否则 UI 会卡在旧的数量。


### 2025-09-25 重新定位浮窗
- 弹窗只覆盖信息列时，左侧 offset 需要与控制面板同宽（360 + spacing），否则会压住按钮。
- 将顶部间距/高度抽成常量，便于针对不同分辨率微调，同时保证 ScrollRect 仍有 420px 的卡面视窗。
- Stretch + offset 的 anchoring 下记得把 sizeDelta 归零，否则旧的 1320×820 仍会干扰布局。

### 2025-09-26 覆盖区域与遮罩
- 弹窗改为顶部锚定后要重新检查 ScrollRect 的 viewport size，否则会继承旧的 820 高度导致可见区域仅剩一行。
- 先调用 ClearDeckViewerOverlayCards 再重建网格，最后记得触发 LayoutRebuilder + 存储 viewport 宽高以支持 LateUpdate 监测。
- 自动化脚本跑完要确认截图刷新，不要依赖旧文件时间戳。

### 2025-09-26 自动化截图目录
- Runner 初始化时先解析基础目录，再以 `<SceneName>_<yyyyMMdd_HHmmss>` 创建子目录，避免覆盖旧图。
- 观察到最新 DeckMana 截图仍未显示卡图，说明 UI 逻辑需进一步排查（怀疑浮窗初始未刷新）。后续调试时优先检查 PopulateDeckViewerOverlayCards().

### 2025-09-27 Addressables 回调日志
- 调整 Addressables.LoadAssetAsync 的 Completed 回调时，务必将日志语句放在条件块之外，否则容易在 if 语句行内插入字符串导致语法错误。
- 如果需要同时记录请求与加载成功，可以先输出请求日志，再依据句柄状态分类输出成功或警告。
### 2025-09-28 ScrollRect 内容刷新
- GridLayoutGroup + ContentSizeFitter 在异步实例化后经常不触发重新计算，必须自行计算列数与行数并写回 sizeDelta。
- 计算高度时记得加上 padding 与 spacing，anchor 设为顶部后只需要改 sizeDelta.y。
- 重新填充后立刻调用 Canvas.ForceUpdateCanvases + LayoutRebuilder，再把 ScrollRect.verticalNormalizedPosition 设为 1，空堆提示也会正确对齐。
- Scrollbar 使用 AutoHide 避免撑开 Viewport，调试时记录 childCount/sizeDelta 有助于快速定位尺寸异常。


### 2025-09-29 自动化回归
- 通过 Unity MCP 控制台执行 `Assets/Refresh` 可以强制触发脚本重编译，清除旧的编译错误缓存。
- DeckViewer 内容高度改为手动计算后，需要先 `Canvas.ForceUpdateCanvases` 再调用 `LayoutRebuilder.ForceRebuildLayoutImmediate`，自动化日志中 `after-resize` 的 sizeDelta=1872 即为验证信号。
- SceneAutomation 的 report.json=success 且截图完整保存在 AutomationOutputs/DeckManaTest，可作为 UI 修复完成的回归依据。

### 2025-09-30 ScrollView 内容自适应
- ScrollRect 的 Content 失去 ContentSizeFitter 后必须手动写 sizeDelta：根据 GridLayoutGroup 的列/行、间距与 padding 计算高度。
- 内容刷新要在浮窗激活且 Viewport 尺寸有效后进行，否则 `rect.width` 为 0 会导致计算列数恒为 1。必要时缓存 ScrollRect 并在填充前强制显示。
- 记录 childCount、rect.size 和首张卡的 Image 状态可以快速判断是高度缺失还是 Sprite 未赋值。调试日志要包在 `#if UNITY_EDITOR` 里避免污染运行时。

### 2025-10-01 ContentSizeFitter 回归
- 手动计算 sizeDelta 虽可行，但当浮窗在填充时仍隐藏会导致列数判断失真；在此场景下直接挂回 ContentSizeFitter 并设 verticalFit=PreferredSize 可以即时撑高 Content，高度不再归零。
- GridLayoutGroup 与 ContentSizeFitter 共存时仍需在 Populate 结束后调用 Canvas.ForceUpdateCanvases + ResetScroll，以免初次打开时停留在底部。

### 2025-10-02 Canvas 与烟雾调试
- 不要复用场景里已有的 Canvas，尤其是 World Space 或 Camera 模式的；浮窗最好自行创建 ScreenSpaceOverlay Canvas 并把 sortingOrder 拉到较高层，彻底排除被其他 UI 遮挡的可能。
- 当 ScrollView 仍无内容时，先用纯 UI Image 构造烟雾测试卡片，可以立即断言布局链路是否正常，比追踪 Addressables 更省时间。
- 填充网格后立刻执行 `Canvas.ForceUpdateCanvases()` 与 `LayoutRebuilder.ForceRebuildLayoutImmediate(GridRoot)`，再调节 ScrollRect 的 normalizedPosition，能在第一帧就看到内容，避免自动化测试截到空白。
- 调试日志要明确输出 child 数量、RectTransform sizeDelta 与首个卡片的 sprite 名称，这些关键信息可以用来区分“资源未加载”和“布局未刷新”这两类问题。

### 2025-10-02 RectTransform sizeDelta 陷阱
- 拉伸锚点配合 offsetMin/offsetMax 定义面板尺寸时不要再把 sizeDelta 设为 0，否则高度会被抵消、内容全部被挤成 0，卡图等子元素即便加载成功也无法显示。
