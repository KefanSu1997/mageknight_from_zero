# DeckIdeal 去紫化与双排卡槽记录

- 目的：消除紫色光晕/发光，改用蓝绿+金色主题，同时在底部提供「出牌区 + 手牌区」两排对齐卡槽。
- 主题资产：重新生成 DeckTheme 全套贴图（背景/边框/卡背/高光/魔法阵），色板改为深蓝背景、银色边框、青色卡背 + 金色点缀；默认配色同步到 `DeckUiThemeCache` 与 `DeckUiBootstrapper`。
- 布局：`DeckUiBootstrapper` 底部使用 VerticalLayout 两行——上方 `BoardSlots` 占位，叠加 `BoardCards`；下方 `HandRow/HandCards` 作为手牌根。卡背/堆叠/浮空卡全部复用同一卡背纹理，牌堆透明度下调，手牌/堆叠高光减弱。
- 场景生成：`DeckIdealSceneBuilder` 适配新路径，增加 `AddBoardRowCards`，手牌/牌堆/魔法阵透明度降低，摄像机背景改深蓝，顶置立绘与宝石使用新青金色光晕；截图输出到 `deck_ideal_001.png`（主/任务目录）。
- 运行时兜底：`DeckIdealRuntimeTuner` 同步新色板、减弱 glow/alpha，手牌根路径兼容新结构，弃牌堆 alpha 下调。
- 怪物素材：通过 Imdream 生成 `monster_arcane_golem.png` + variant，顶置立绘与场上示例使用新素材；任务截图目录内保存 `monster_assets_001/002.png` 作为预览。
- 调试要点：如 MCP 报 “Transport closed”，等待 ≥15s 再重试；截图可用菜单 `Tools/Scene Builders/Build + Capture Deck Ideal`，输出路径已包含任务 runs 目录。
