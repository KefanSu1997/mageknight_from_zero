高级牌组手牌功能演示场景设置指南

=== 场景准备 ===
1. 打开 advanced_hand_deck_demo 场景
2. 删除原来的 HandManager 组件
3. 添加 HandManagerAdvanced 组件
4. 设置必要的引用：
   - deck: 牌组对象 (DeckRuntime 组件)
   - cardPrefab: 卡牌预制件 (CardRuntime 预制件)
   - handRoot: 手牌容器

=== UI按钮设置 ===
1. 创建 Canvas
2. 添加5个按钮：
   - 抽牌按钮 "Draw 1"
   - 弃牌按钮 "Discard Selected"
   - 出牌按钮 "Play Selected"
   - 洗牌按钮 "Shuffle Deck"
   - 回收弃牌按钮 "Recycle Discard"
3. 添加文本显示：
   - 牌组数量显示
   - 弃牌堆数量显示
   - 状态信息

=== 使用说明 ===
这个演示场景实现了完整的牌组和手牌管理功能：
- 点击卡牌选择/取消选择
- 点击