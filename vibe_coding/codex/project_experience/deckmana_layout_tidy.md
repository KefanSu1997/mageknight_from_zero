# DeckMana 桌面布局调优记录

## 背景
- 进入 `Part1_DeckManaTest` 时发现：
  1. 手牌散落在牌桌上方，未沿 Slot 排列。
  2. 牌组/弃牌按钮被手牌遮挡，很难点击。
  3. 桌面背景与新牌桌存在明显夹角，观感不统一。

## 关键调整
1. **手牌重新贴合 Slot**
   - 给 `DeckUiHandPresenter` 增补 `ForceSnapLayout()`，重新收集手牌并立即调用 `SnapCardToSlot`；
   - 在 `Part1SceneHarness.BindHandRoot` 中调用该方法，确保从旧 Canvas 迁移过来的手牌也能瞬间排版。

2. **牌桌结构重排**
   - 将 `DeckUiTableBuilder` 中的桌面、牌组/弃牌壳体、手牌托盘倾角分别降低（24°/16°/18°），同时上移牌组/弃牌壳体，避免与手牌重叠；
   - 调整各自的 padding 与 size，使按钮始终浮于手牌之上。

3. **背景贴图对齐**
   - 如果主题提供背景贴图，直接赋给桌面底板 (`SurfaceBase`) 并保持与桌面相同的倾角；
   - 外层 Canvas 背景改为半透明主题色，去除原本的第二层立面，消除“两个平面夹角”现象。

## 验证步骤
- `Assets/Refresh` 重新编译，Unity Console 0 报错；
- 手动进入 Play 模式确认：
  - 手牌沿 7 个 Slot 排成扇形；
  - 牌组与弃牌按钮位于手牌上方，可直接点击；
  - 背景图与卡桌保持同一透视角度。

## 建议
- 如需再降低倾角，可调整 `TableTiltAngle`/`DeckZoneTiltAngle`/`HandStripTiltAngle` 常量；
- 自动化截图需在非 Play 模式下触发，避免编辑器报“请在编辑模式下启动自动化执行”。

### 2025-10-20 补充
- 将桌面倾角从 32° 降至 18°，并把牌组/弃牌区抬升至桌面上半部分，背景 `Image` 设置为 `raycastTarget = false`，恢复按钮可点与闪光反馈。
- 手牌托盘移出倾斜桌面，锚定在屏幕底部；`DeckUiHandPresenter` 设置零角度展开并提供 `ForceSnapLayout()`，保证顶视相机下卡面垂直展示。
- 测试控制列新增折叠按钮 `Btn_ToggleActionPanel`，可在调试时快速收起测试按钮，让中央牌桌留出空间。
