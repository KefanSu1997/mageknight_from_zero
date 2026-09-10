# Element Card 横向暗带：卡面比例与 Preserve Aspect (2026-01-14)

## 现象
- ElementCards 翻面截图出现横向暗带/残影，前后面切换时卡面上下出现可见条带。

## 原因
- 卡面 PNG 比例 (例如 740x1039) 与卡牌 RectTransform (280x420) 不一致。
- `Image.preserveAspect = true` 会导致上下留空，底色显暗。

## 解决
- 在 `ElementCardView` 中统一将 front/back 的 `preserveAspect` 设为 `false`，确保卡面铺满卡牌矩形。
- 同时禁用内容层 `RectMask2D`，避免不必要的裁切与叠层干扰。

## 影响文件
- `Assets/Scripts/UI/ElementCardView.cs`
