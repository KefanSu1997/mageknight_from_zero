# DeckMana 场景 UI 重构记录

## 背景
- 用户希望 DeckManaTest 场景的控制按钮、魔晶池、操作日志、手牌分别占据固定的红/绿/黄/蓝区域。
- 原来的 Part1SceneHarness 使用横向布局组，仅能并排放置按钮列与信息列，无法兼顾底部手牌与滚动日志。
- 日志采用纯 TextMeshProUGUI 累积文本，内容较长时会溢出且难以回顾历史记录。

## 处理过程
1. 改写 Part1SceneHarness，使用锚点和偏移量精确定位左侧按钮列、右侧信息列与底部手牌区，保持自动化按钮节点路径不变。
2. 为操作日志创建 ScrollRect + Scrollbar 结构，配合 ContentSizeFitter 自动扩展文本高度，并在 Part1TestManager 中追加日志后强制滚动到底部。
3. 新增 HandArea 容器并绑定 HandManager.handRoot，保证抽牌实例化的卡牌始终贴合蓝色区域，窗口缩放时依旧居中。
4. 调整魔晶面板配色和 LayoutElement 参数，让视觉层级符合规划，同时保留 ConfigureManaDisplay 复用逻辑。

## 经验
- 当 UI 需要同时满足纵向与横向分区时，锚点 + 偏移量比 LayoutGroup 更灵活，可精确留出区域间距。
- ScrollRect 如果使用 Mask 组件需要非零 Alpha，推荐改用 RectMask2D 避免透明视图导致内容被全部裁切。
- 如果 UI 初始化后需要保持手牌排布稳定，可在首次排列时缓存锚点模板，后续重排直接复用，避免随机 spacing 导致视觉跳动。
- ScrollRect 要稳定工作，需要为文本内容设置顶部锚点和 ContentSizeFitter，否则滚动条不会响应文本高度变化。
- 重构自动生成 UI 时应优先确认自动化测试依赖的对象路径，避免因层级变化破坏既有脚本和配置。

## 教训
- 修改大型 UI 架构前应先梳理所有脚本与编辑器工具的引用关系，特别是自动化测试脚本中的节点路径。
- 当日志文本量大时必须引入滚动容器或分页方案，否则调试信息很快会淹没关键提示。
- 手牌容器需要明确的锚点与高度，否则 HandManager 的排列算法会因为父节点尺寸异常而错位。


## 案例复盘：手牌偏移顽疾（2025-09-23）

### 问题特征
- DeckManaTest 场景中点击“测试牌库系统”后，手牌区域的卡牌会整体漂移，并且随着重复测试不断偏离中心。
- 自动化截图显示初始布局与点击按钮后的手牌位置不一致，Console 日志也未给出明确异常。

### 根因分析
1. `HandManager.ClearHand()` 在重置手牌时直接 `Destroy` 子节点，Unity 会在下一个帧才真正移除 GameObject，导致布局模板仍被旧 RectTransform 占位。
2. `HandManager.ArrangeCards()` 每次都根据当前子节点数量重新计算宽度和锚点，没有复用初始布局坐标，随机感的 spacing 让布局结果缓慢漂移。
3. 调试手段不足：原来的 `Part1TestManager` 没有记录手牌锚点，一旦布局异常难以追踪具体坐标变化。

### 解决方案
- 为 HandManager 增加锚点模板缓存机制：首次布局时记录每张卡的 `anchoredPosition`，后续在卡牌数量不变的情况下直接复用缓存，确保位置稳定。
- 改写 `ClearHand()`，先把旧卡牌从 `handRoot` 脱离再销毁，避免 Unity 延迟销毁导致的引用残留。
- 在 `Part1TestManager` 中加入仅编辑器下可见的 `LogHandLayout` 方法，测试动作前后输出每张卡的坐标，便于快速比对与回归验证。
- 通过自动化测试（SceneAutomation）跑回归，确认日志显示“5 cards”且坐标与初始一致。

### 经验与教训
- Unity UI 中批量销毁子节点时需注意延迟销毁，若布局依赖 `childCount` 或 `GetChild`，必须确保旧引用被彻底移除。
- 对用户要求的“固定区域”类 UI 布局，唯一可行的方案是显式记录锚点或使用 Layout 组件，不要依赖同一套算法重复运算。
- 调试日志要贴近问题本体：这里输出具体 anchor 与 size 远比单纯提示“完成重排”有用。
- 自动化脚本既能重放，也能提供稳定截图，是验证 UI 修复可信度的重要工具，应当配置好路径后常态化使用。
- 一旦遇到耗时较长的问题，及时把过程记录到 `vibe_coding/codex/project_experience`，下次排查类似现象可以直接对照。

## 2025-10-09 调整记录

- DeckViewerOverlay 扩展到左右各 40px 内边距、720px 高度后，ScrollRect Viewport 能完整容纳 3 列卡图，避免旧版 4 列导致的缩放模糊。
- GridLayout cellSize 提升至 320x480，Spacing 设置 (14,24)，搭配 Part1TestManager 的列数上限 3，卡面比例恢复接近原始美术尺寸。
- DeckZonesPanel 的按钮高度改为 140px，Viewer 区域最小高度 240px，可在浮窗打开时保留明显的卡图展示窗口。
- 自动化回归 (20251009_223321) 截图显示卡图高度约增加三分之一，说明布局扩展对最终呈现生效。

## 2025-10-10 一次性拉满卡区

- 标题区重构为固定 64px、高亮背景 + Padding，使正文 ScrollRect 起点与蓝框一致，卡图展示区域完整覆盖红框。
- 正文改用 24px 内边距、GridLayout spacing=(24,32)，cellSize 提升到 340x510，同时将 Part1TestManager 的列数上限增至 6、卡宽限制设为 320~340，视窗会按可用宽度自适应放入更多卡片。
- 调整后自动化截图 `Part1_DeckManaTest_20251010_001231/001_打开牌组浮窗.png` 满足预期；调试日志显示 `cellSize=(340,510)`，内容高度 1203 确保 ScrollRect 可滚动。
- 经验：在 ScreenSpaceOverlay 中拉大卡面时，要同步放宽 grid padding/spa­cing，否则卡片仍会被 ContentSizeFitter 约束导致实际区域偏小。
