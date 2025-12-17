# Deck UI Overlay 与手牌动效集成笔记

## DeckViewer 卡槽统一主题
- 在 `Part1TestManager` 内新增 `DeckViewerCardVisual` 结构与 `CreateDeckViewerCardVisual` 工厂，将卡框、遮罩、正文标签、主题色一次性搭起，确保 Addressables 卡面加载前也有卡背占位。
- 背景、卡框与高光分别调用 `DeckUiThemeCache` 的 `BorderSprite` / `CardBackSprite` / `CardHighlightSprite`，若素材缺失退回到主题色块。注意 `RectMask2D` 需要挂在包裹容器，避免 Sprite 拉伸溢出。
- `PopulateDeckViewerOverlayCards` 改为维护 `List<DeckViewerCardVisual>`，释放 Addressables 资源后销毁根节点即可；日志沿用旧的加载信息，便于排查卡面损坏。

## 手牌演出与 Presenter 协调
- `DeckUiHandCardAnimation` 只保存偏移、角度、缩放目标，实际变换由 `DeckUiHandPresenter` 在 `MoveCardTowardsSlot` 与 `SnapCardToSlot` 内叠加，避免 LateUpdate 相互抢写。
- 动画脚本在 `CollectCards` 时自动挂载（仅限存在 `CardRuntime` 的子节点），并调用 `TriggerReveal(true)` 做首次翻入，后续通过指针事件平滑抬升与按压。
- 计算顺序：目标锚点 → 动画偏移 → 最终角度/缩放；若处于悬停/按压则强制 `SetAsLastSibling`，其余维持原始顺序，解决翻牌后卡片被遮挡的问题。
- 翻牌实现：`CardRuntime` 缓存正面 Sprite，`DeckUiHandCardAnimation` 在 Reveal 阶段先调用 `ShowCardBack`（使用 `DeckUiThemeCache.CardBackSprite`），随后将 `YRotationOffset` 从 180° 过渡到 0°，在过半时切换回正面，Presenter 组合 Y 轴与 Z 轴旋转即可渲染 3D 翻牌手感。
- 高光处理：动画脚本直接控制 `CardRuntime.SelectionHighlight` Alpha，指针进入/按压分别拉升目标值，`highlightFadeSpeed` 负责缓动，不额外依赖 DOTween。

## 牌堆高光 Pulse
- 新增 `DeckUiGlowPulse` 组件，接收 `MinAlpha/MaxAlpha` 与 `PulseSpeed`，通过 `Time.unscaledDeltaTime` 做正弦插值；停用时重置为最低透明度，避免闪烁残留。
- `DeckUiBootstrapper` 在 `CreateDeckStack` / `CreateDiscardGhost` 里追加高光 Image（type 设为 Sliced），并立即配置 `GlowPulse`，实现主题一致的呼吸效果。

## 自动化配置同步
- 所有 Part1 场景的按钮均迁移至 `Canvas/Part1TableRoot/ActionColumnRoot/Buttons/...`，对应 Deck/Discard 区域则位于 `.../DeckZoneRoot/*Anchor/*`。更新 Automation JSON 后，批量截图才能覆盖最新布局。
- 若需在 Hand Deck 独立场景复用布局，务必在运行时通过 `DeckUiTableBuilder` 搭建 `Part1TableRoot`，否则自动化路径会再次失效。
