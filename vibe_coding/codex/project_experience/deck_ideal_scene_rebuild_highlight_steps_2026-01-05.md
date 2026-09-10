# DeckIdeal 场景重建 + 自动化高亮步骤经验（2026-01-05）

## 问题
- DeckIdeal 自动化配置 steps 为空，report 与截图无法追溯，001/002/003 几乎一致。
- Part1_DeckIdeal 场景构建仅保留背景与计数牌，卡背/卡面不可见。

## 处理要点
- 在 `DeckIdealSceneBuilder.BuildStaticLayout` 中恢复完整布局（牌堆/弃牌/手牌/底部条带/边框/中心法阵）。
- 增加 Deck/Discard/Hand 高亮覆盖层，并在点击时切换显示，保证步骤截图可区分。
- 自动化配置中补齐 steps（buttonPath 指向 `UIRoot/Overlay/*Hitbox`），并调低 wait 以加快跑图。
- 运行 `Tools/Scene Builders/Build + Capture Deck Ideal` 重新生成场景，再跑 `Tools/Scene Automation/Run DeckIdeal Automation`，避免拷贝旧 report。

## 结论
通过恢复场景构建与点击高亮，DeckIdeal 自动化产物可追溯且截图包含可辨识卡背/卡面与场地纹理。
