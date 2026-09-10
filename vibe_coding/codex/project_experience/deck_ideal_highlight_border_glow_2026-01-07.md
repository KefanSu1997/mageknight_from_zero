# DeckIdeal 高亮与卡面细节强化记录（2026-01-07）

## 背景
高亮表现被判定为“填充型遮罩”，并在手牌高亮态洗掉卡面细节；同时卡面金属镶边/符文/星尘细节不足。

## 处理要点
1. 高亮改为“透明中心的边框 + 外发光”
   - 高亮视觉层移动到卡牌之下（HighlightRoot 置于卡牌节点之前）。
   - Glow 使用双层外发光 + Outline，全部 fillCenter=false，确保中心透明。
2. 手牌卡面层次增强
   - 增加符文环层级：外/中/内/核心四层，并添加上下弧形符文。
   - 金属镶边加厚：Frame + Accent + InnerFrame 多层边框，并加 Outline 强化金属高光。
   - 星尘粒子改为更密集小点，使用白色 sprite + Shadow 形成星光闪烁。
3. 避免洗色
   - CardFaceGlow / Filigree 采用边框型 fillCenter=false。
   - 高亮 alpha 适度下调，避免覆盖卡面。

## 文件与位置
- 主要实现：`Assets/Editor/DeckIdealSceneBuilder.cs`
- 高亮控制：`Assets/Scripts/UI/Part1/DeckIdealHitboxHighlighter.cs`
