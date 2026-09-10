# ElementCardsShowcase 场景空白：Canvas RectTransform 缩放为 0

## 现象
- 进入 `Part1_ElementCardsShowcase` 场景后画面完全空白。

## 根因
- 场景中的 `Canvas` RectTransform `m_LocalScale` 被写成 `{x:0,y:0,z:0}`，导致整棵 UI 树被缩放到不可见。
- 同时锚点 `m_AnchorMax` 与 `m_Pivot` 为 0，进一步导致布局不合理。

## 解决方案
- 将 `Canvas` RectTransform 参数恢复为全屏常规值：
  - `m_LocalScale` -> `{x:1,y:1,z:1}`
  - `m_AnchorMax` -> `{x:1,y:1}`
  - `m_Pivot` -> `{x:0.5,y:0.5}`

## 验证方式
- Unity Console 编译错误为 0。
- 场景 UI 可见性恢复。

## 经验要点
- 遇到“场景完全空白”时，优先检查 Canvas/RectTransform 的缩放与锚点；`m_LocalScale=0` 是致命配置。
