## 理想卡组截图调优（DeckIdealSceneBuilder）

- **症状**：顶部立绘高光使用卡牌高亮贴图导致黄色实心方块覆盖中心；星云与魔法阵叠层过多令画面泛白，左右牌堆过于实心。
- **修复要点**：
  - 顶部立绘选用英雄卡面（`advanced_card_000/003/004.png`），高光透明度降至 0.28 并缩小范围，保持 `preserveAspect`。
  - 星云、魔法阵整体 alpha 下调（背景 0.45、魔法阵 0.62），避免双重背景导致泛灰。
  - 左右牌堆锚点靠近上缘，右堆 alpha 0.5 表现“透明”堆，数量 4–5 张，旋转轻微。
  - 手牌列整体下沉 30px，间距 238px，金色高光减弱到 0.58，角度微调保持扇形。
  - 调整后运行菜单 `Tools/Scene Builders/Build + Capture Deck Ideal` 重新生成场景与截图。
- **验证**：确认自动生成的 `multi-agent-workspace/review_bundle/artifacts/screenshots/part1_deck_ideal_001.png` 无大色块遮挡，布局与紫银主题参考图一致。
