# DeckIdeal 叠层与卡面风格统一记录（2026-01-06）

## 背景
- Review 指出 DeckIdeal 场景存在大面积半透明叠层导致手牌发灰、信息可读性下降。
- 同时卡背/场地已偏暗黑奇幻，但卡面仍偏浅色纸质风格，风格不统一。

## 调整策略
- 叠层透明度收敛：优先降低 `MagicSwirl` 与 `BottomBand` 的 alpha，保留轻微氛围但避免遮罩压灰卡面。
- 卡面整体色调下压：对 `HandCard_*/Face/Art` 做统一暗化色调，让旧卡面与暗黑场景更协调。
- 金属框与装饰统一：将 Frame/Filigree 从偏亮色改为暗金，减少“旧纸张”观感。

## 关键参数（可复用）
- `MagicSwirl` alpha: 0.24 -> 0.08
- `MagicCircleOverlay` alpha: 0.12 -> 0.06
- `BottomBand/Base` alpha: 0.12 -> 0.04
- `BottomBand/Rim` alpha: 0.12 -> 0.05
- `HandCard_*/Face/Art` color: (0.78, 0.76, 0.86, 1.0)
- `HandCard_*/Face/Frame` color: (0.78, 0.62, 0.35, 0.85)
- `HandCard_*/Filigree` color: (0.80, 0.68, 0.40, 0.25)

## 经验与教训
- 大面积半透明叠层比“强对比背景”更容易造成卡面发灰，优先收敛 alpha。
- 卡面素材无法立即替换时，可先用统一色调压暗与暗金框线过渡风格，保持布局稳定。
