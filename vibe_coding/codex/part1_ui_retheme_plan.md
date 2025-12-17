# Part1 测试场景 UI 重构方案（草案）

## 目标
- 在 Deck 主题背景上重建牌桌布局，让 Deck/Mana/Combat/Exploration/Recruitment/FullFlow 测试模块视觉统一。
- 维持现有测试按钮功能、日志输出、自动化场景路径。
- 支持逐步扩展动效（卡牌翻转、魔晶闪烁）与更多主题。

## 全局布局概念
1. **底层背景**：保持 `DeckUiBootstrapper` 输出的星云/魔法阵，后续为主题切换预留接口。
2. **牌桌主体（新建脚本 DeckUiTableBuilder）**
   - 顶部：`DeckStack` 与 `DiscardPile` 两块立体牌堆；数量铭牌嵌入堆底。
   - 中央：`InfoCanvas`，根据场景显示 Combat/Exploration/Recruitment 板块，采用卷轴/木牌布局。
   - 左侧：`ActionColumn`，统一按钮面板（带图标）。
   - 底部：`HandStrip`，7~8 张手牌槽，默认展示卡背，支持 hover 翻面。
   - 右下角：`ManaReservoir`，瓶状魔晶槽 + 数字。
   - 前景：粒子/辉光装饰。
3. **浮层组件**
   - DeckViewerOverlay 升级为半透明卷轴，网格卡片应用统一卡框。
   - 通用弹窗（后续战斗结果等）可共享此框架。

## 模块拆解

### 1. 按钮系统
- 资源：按钮底图三态、功能图标（Deck/Mana/Combat/Exploration/Recruitment/Flow）。
- 脚本改动：`Part1SceneHarness.CreateButton` 生成 `Button` + `Image` + `TMP` + 图标。
- 动效：hover 缩放、点击回弹、发光轮廓。

### 2. 牌堆 & 弃牌
- 资源：牌堆底座、卡背变体、发光纹理。
- 布局：三层卡片偏移；数量文本置于木牌。
- 交互：点击触发现有 deck/discard 逻辑；后续可加动画。

### 3. 手牌区
- 资源：手牌槽卡框、卡背/卡面统一材质。
- 布局：扇形/等距排布；支持 `HandManager` 写入。
- 脚本：新增 `DeckUiHandPresenter` 协调 `HandManager` 生成的卡牌 RectTransform，附加翻转脚本。

### 4. Mana Reservoir
- 资源：魔晶瓶插画、背景面板、微光粒子。
- 布局：水平排布 5 个瓶子；数值覆盖在瓶下铭牌；支持 Gold 追加插槽。
- 脚本：调整 `ConfigureManaDisplay` 传入新 UI 元素，保留数值更新逻辑。

### 5. Combat 面板
- 资源：双侧角色卡牌框、顶部横幅、按钮纹理。
- 布局：中央卷轴展示场景说明，下方按钮；英雄/敌人用卡槽显示头像+属性列表。
- 脚本：`ConfigureCombatPanel` 绑定结构不变，仅更新 UI 生成。

### 6. Exploration & Recruitment
- Exploration：六边形地图置于卷轴中央；右侧显示摘要。
- Recruitment：左/右木牌展示待招募/已招募；中间地区标签。
- 均需换肤及图标套件。

### 7. 日志区域
- 资源：卷轴背景、滚动条。
- 功能：保留可滚动文本，附加分隔线样式。

## 迭代步骤（建议顺序）
1. **基础架构调整**
   - 重构 `Part1SceneHarness` 生成结构，替换方框背景 → 空容器 + 新组件（先用占位色块）。
   - 落地 Deck/Mana/手牌/按钮容器位置。
2. **按钮 & 牌堆改造**
   - 完成按钮统一样式 + 交互测试。
   - 构建 Deck/Discard 堆叠 UI + 数量铭牌，验证点击事件。
3. **手牌整合**
   - 实装手牌槽框圆角 + 卡背资源。
   - 编写 `DeckUiHandPresenter` 与翻转脚本（暂提供预留 API）。
4. **Mana Reservoir**
   - 替换数值容器 → 魔晶瓶 UI。
   - 调整 `ConfigureManaDisplay` 对应元素。
5. **各功能面板主题化**
   - Combat、Exploration、Recruitment 逐一替换为卷轴/木牌布局。
   - DeckViewerOverlay 更新为统一卡框。
6. **动效与细节**
   - 粒子特效、hover 动画、数值闪光。
   - 检查自动化路径、日志输出是否受影响。
7. **集成与验证**
   - 四个 Part1 场景逐个检查显示模块。
   - 跑一次 Deck/Mana/Combat 等自动化测试。
   - 更新工作日志与经验文档。

## 当前进展（2025-10-18）
- ✅ 完成步骤 1 & 2：`DeckUiTableBuilder` 搭建统一骨架，按钮统一样式并加入动效，牌堆/弃牌区改为堆叠视觉。
- ✅ Combat / Exploration / Recruitment / 日志面板完成主题化改造，DeckViewer Header 与按钮样式保持一致。
- ✅ 初版 `DeckUiHandPresenter` 已接入，手牌可贴合槽位展示，等待后续翻牌与发光动效。
- ⏳ 下一步：细化 DeckViewer Overlay、补充手牌翻转与牌堆发光动画，并同步自动化脚本节点。

## 素材需求（优先级）
1. 按钮底图 + 功能图标（6 枚）。
2. 牌堆 & 手牌框（卡背正面、发光边框）。
3. 魔晶瓶 & 背景。
4. 卷轴/木牌背景：Combat、Exploration、Recruitment、日志。
5. DeckViewer 卡框。
6. 动效粒子、hover 发光。

> 备注：若即梦生成结果需后期加工，可先按占位色块实装，确认布局无误后统一换真实素材。

---

## 待与 hajimi 确认的事项
1. **总体布局**：左侧按钮列 + 中央信息卷轴 + 底部手牌条 + 顶部牌堆的结构是否符合预期？是否需要增加额外信息区域（例如状态条、提示条）？
2. **素材风格**：按钮与面板采用“紫金 + 魔法卷轴”方案是否合适？是否需要额外风格指引（如偏写实/偏卡通）？
3. **动效范围**：首轮迭代是否实现手牌翻牌、牌堆发光、魔晶闪烁等动效？若时间有限，优先级如何排序？
4. **自动化依赖**：现有 Scene Automation 步骤需要保留按钮名称与层级，是否允许在 UI 重构后调整节点命名？如需占位节点以兼容现有测试，请提前确认。
5. **资源交付**：若即梦生成的贴图需要额外后处理（抠图、9 切片），是否由我处理还是等待美术支持？

请 hajimi 查看以上草案与问题，给出反馈后再进入实施阶段。***
