# Deck Ideal 素材替换规划要点（卡背/卡面/场地）

## 资产定位与尺寸
- 卡背（Deck Ideal 主图）: `Assets/UI/Images/DeckTheme/Ideal/deck_ideal_card_back_v2.png`（640x880）
- 卡面（Deck Ideal 统一卡面纹理）: `Assets/UI/Images/DeckTheme/Ideal/deck_ideal_card_face_v1.png`（1024x1024）
- 场地/背景（Deck Ideal 背板）: `Assets/UI/Images/DeckTheme/Ideal/deck_ideal_full_1378x1204_v2.png`（1378x1204）
- 通用卡背/背景（用于回退或其他场景）:
  - `Assets/UI/Images/DeckTheme/deckui_card_back.jpg`（1024x1024）
  - `Assets/UI/Images/DeckTheme/deckui_background.jpg`（1024x1024）

## 引用位置（场景/代码）
- `Assets/Editor/DeckIdealSceneBuilder.cs`:
  - `ThemeBackgroundPath` -> `deck_ideal_full_1378x1204_v2.png`
  - `ThemeCardBackPath` -> `deck_ideal_card_back_v2.png`
- `Assets/Scripts/UI/Part1/DeckUiThemeCache.cs`:
  - 回退路径使用 `deckui_background.jpg` / `deckui_card_back.jpg` 等
- `Assets/Prefabs/CardView.prefab`:
  - `CardRuntime` 运行时加载 `CardSO.ImagePath`（Addressables），前景卡图来自 `Assets/GameData/cards/*.png`

## 替换策略
- 保持分辨率一致，避免缩放带来的锯齿/模糊:
  - 卡背 640x880，卡面 1024x1024，场地 1378x1204
- 新图先落在 `Assets/UI/Images/DeckTheme/Ideal/Candidates/*`，评估后再覆盖最终路径。
- 画面关键词统一：黑暗/奇幻/流沙/闪烁/星光/旋涡/魔法，保持卡背与场地视觉语言一致。

## 经验与提醒
- Deck Ideal 场景通过 SceneBuilder 直接按路径加载素材，替换时优先检查 `DeckIdealSceneBuilder.cs` 的路径常量。
- 卡面若要引入新的“统一纹理”，建议先改用候选路径替换 `deck_ideal_card_face_v1.png`，确保色彩与卡背一致。
