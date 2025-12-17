# Deck UI 手牌排序与自动化执行记录

## 背景
- 手牌展示切换到俯视布局后，`DeckUiHandPresenter` 以首见顺序缓存 `RectTransform`，在鼠标快速扫动时会复用 `GetSiblingIndex` 回退，导致局部交换动画反复触发。
- DeckMana 自动化在新增隐藏按钮后可完整跑通，但 Play 模式被暂停或中途执行调试菜单，会导致步骤停留在首帧，仅生成 `000_Scene Start`.

## 处理方案
- 在 `CollectCards()` 内使用 `List.Sort` 配合自定义比较器，优先按历史布局序，再回退到当前 `SiblingIndex`；同步重写 `cardLayoutOrder`，确保 Presenter、动画与 sibling 顺序一致。
- 桌面俯视感：为牌堆区域添加 `DeckZoneTiltAngle` (-5.5°) 与手牌带 `HandStripTiltAngle` (-10°)，同时收窄牌堆 anchor 偏移，贴合参考图的高低差。
- 自动化执行时保持 Editor 处于 Edit Mode 且不插入调试菜单，待 `Automation finished` / 报告刷新后再退出 Play；若必须调试，先 `Edit/Pause`，收集日志后恢复。

## 验证
- Unity 控制台无编译错误，DeckMana 自动化输出 `Part1_DeckManaTest_20251018_234552`，7 张截图全部更新。
- 快速移动与交错悬停手牌时不再出现卡序高速抖动，动画叠层保持正确。

## TODO
- 后续将 Tilt 参数抽取到 Theme 配置，以不同桌面风格快速切换。
- 复用自动化测试于其他 Part1 场景，确认俯视布局对战斗/招募流程无偏移。
