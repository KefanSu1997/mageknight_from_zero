# ElementCardsShowcase 白块：背面/边框缺失时的前面回退

## 现象
- 场景只显示白色方块，无卡牌图。

## 根因
- `Assets/UI/Images/ElementCards/element_card_back_*` 与 `element_card_frame_*` 资源不存在，导致 Img_Back / Img_Frame 的 Sprite 引用缺失。

## 解决方案
- 初始化时默认展示正面（front）。
- 若背面 Sprite 未加载到，则回退使用正面 Sprite，避免白块。
- 场景内 `showFrontOnStart` 设为 1，避免 Bootstrapper 未触发时仍显示背面白块。
- 缺边框时可用“卡背生成边框”方案：
  - 以卡背为底，做轻微模糊 + 透明中心遮罩（软边），形成自然融合的边框覆盖。
  - 输出到 `Assets/UI/Images/ElementCards/element_card_frame_*_imdream.png`。

## 影响
- 即使背面/边框资源缺失，也能保证卡牌可见。
