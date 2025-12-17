# DeckIdeal 层级与宝石强化记录

- 目标：让 Part1_DeckIdeal 更贴近紫银召唤参考图，避免魔法阵覆盖牌面、四角宝石不明显、顶部立绘缺失。
- 主要调整：
  - 框体：银框不透明度提升，阴影加深，filigree 透明度提高并保留呼吸/旋转；宝石尺寸扩大到 180px，颜色饱和度提高并放在最前层。
  - 中心层级：魔法阵/光晕/粒子移动到靠后层（SetSiblingIndex 1~3），减少对手牌与立绘的遮挡；魔法阵 alpha 提高到 0.55 保持存在感。
  - 顶部立绘：改用 monster(11) 资源，附带更大的柔光 (680x400)，并强制置顶 + 漂浮动效。
  - 漂浮与牌堆：漂浮卡片重新偏移/缩放，叠加金色高光；左右牌堆张数与透明度差异化，并维持 CanvasGroup 兜底。
  - 手牌：手牌容器置顶，金色 filigree/glow 叠层，spacing 微调为 242 保持 5 张对齐。
- 调试要点：
  - 运行时兜底由 `DeckIdealRuntimeTuner` 完成，若编辑器构建缺层，可直接进 Play 检查宝石/立绘/魔法阵是否补齐。
  - 若 MCP 报 Transport closed，可等待 15s 再调用 Assets/Refresh 或 Run Deck Ideal Automation。
