# DeckIdeal 半透明矩形遮挡排查与修复（2026-01-07）

## 问题现象
- DeckIdeal 自动化截图在交互态出现大面积半透明矩形遮挡（顶部两块 + 中央一块 + 底部横条）。
- 重点复现：`deck_ideal_after.png` 与 `003_HighlightHand.png`（旧版本截图）。

## 根因判断
- 高亮图与魔法阵图为 JPG（无透明通道），当 CanvasGroup 或 Image alpha 被拉起时会以矩形方式覆盖画面。
- Overlay 下的 Deck/Discard/Hand 高亮 + CenterStage 的 MagicCircle/Swirl 叠层在交互时被激活，导致遮挡。

## 修复策略
1) 高亮层彻底禁用
   - `DeckIdealHitboxHighlighter` 中对所有高亮组强制 `CanvasGroup.alpha=0`，并将 Image.alpha 置 0 且 `image.enabled=false`。
   - `DeckIdealSceneBuilder.CreateHighlight` 创建高亮 Image 后直接禁用渲染。

2) 中央魔法阵/旋涡禁用
   - `DeckIdealSceneBuilder.BuildCenterStage` 中 `MagicCircleOverlay` 与 `MagicSwirl` 设置 `image.enabled=false`，避免 JPG 叠层矩形。

## 验证方法
- 重新执行 `Tools/Scene Builders/Build + Capture Deck Ideal` 生成 `deck_ideal_overview.png`。
- 运行 `Tools/Scene Automation/Run DeckIdeal Automation`，核对 `000/001/002/003` 无矩形遮挡。
- 若仍存在遮挡，继续检查 `DeckIdealRuntimeTuner` 中的 `MagicAura/CenterParticleGlow` 等大尺寸 Image 是否被启用。

## 可复用经验
- 任何大尺寸 Overlay 使用 JPG 时，必须确认 alpha=0 或直接禁用 Image，否则交互态极易形成半透明矩形遮挡。
- 高亮仅用于交互反馈时，应优先采用有透明通道的素材；没有透明通道的素材应禁用或替换。
