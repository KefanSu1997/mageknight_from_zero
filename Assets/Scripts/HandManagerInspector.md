# HandManager使用指南

需要在Unity编辑器中配置以下内容：

## 必需变量设置
- **deck**: 拖入DeckRuntime组件
- **cardPrefab**: 拖入CardView.prefab预制体
- **handRoot**: 新建一个空物体作为手牌容器

## handRoot配置
1. 在场景中新建空物体命名为"HandParent"
2. 添加RectTransform组件（如果是Canvas子物体）
3. 将HandParent拖到HandManager的handRoot字段
4. 确保handRoot中心点位于屏幕中央，卡牌会在此容器内水平排列

## 测试功能
- 在HandManager组件上点击右键->"Arrange Cards"可以手动重新排列当前手牌
- 启动游戏后会自动抽取5张卡牌并按水平排列显示