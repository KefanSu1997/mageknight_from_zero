## DeckIdeal 紫银主题快速重染与堆叠统一卡背

- **症状/需求**：验收要求去除浅蓝透明叠层、统一牌背为紫金风格，并替换场中怪物。原主题贴图与默认色板偏青蓝，导致截图与参考差距大。
- **解决步骤**：
  1) 直接用 Pillow 按通道比例+偏移重染 DeckTheme 纹理，避免额外素材依赖：`deckui_background`/`border`/`magic_circle`/`card_back`/`card_highlight` 分别用 RGB 乘数与偏移靠拢深紫+银+金。
  2) 将 `DeckUiThemeCache` 默认颜色改为深紫背景、银色边框、紫色高光、金色次强调，让运行时兜底色与新贴图一致。
  3) 在 `DeckIdealSceneBuilder`/`DeckIdealRuntimeTuner` 中同步色值，堆叠、前景光效、宝石、顶置立绘 glow 统一紫金；手牌与场上卡列使用怪物(20/65/52/3/36)及紫背卡框，确保牌背一致。
- **注意事项**：
  - 无 numpy 环境时，可用 Pillow 的 `point` 通道变换实现乘法+偏移。
  - 重染贴图后务必重跑 `Tools/Scene Builders/Build + Capture Deck Ideal`，否则截图仍是旧色；若 MCP 反复 “Transport closed”，按指南每 15s 重试并记录未完成项，等待连接恢复。
  - 牌堆/漂浮卡背要统一使用 `DeckUiThemeCache.CardBackSprite`，同时调整 glow 颜色至金色系，避免再次出现蓝背/紫背混用。
