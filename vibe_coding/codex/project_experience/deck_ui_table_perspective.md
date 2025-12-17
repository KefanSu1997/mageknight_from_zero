# Part1 牌桌透视化改造心得

## 背景
- Checklist 要求将测试场景的平面色块升级为“桌面物件”式布局，并解决按钮文字被裁切、俯视镜头缺乏景深等问题。
- 既有 `DeckUiTableBuilder` 只生成矩形容器，`Part1SceneHarness` 基于这些容器临时填充 UI；通过脚本改造即可统一风格、减少场景改 YAML 的成本。

## 关键改动
1. **重建桌面骨架**  
   - `DeckUiTableBuilder` 拆分出桌面框架、信息卷轴、左侧功能柱、底部手牌槽，加入牌堆/弃牌展示壳体与魔法阵背景。  
   - 靠 `DeckUiThemeCache` 调用主题纹理，附加 Outline/Shadow 让卡槽、卷轴像摆在桌面上的实体。
2. **手牌槽与按钮升级**  
   - 扩展 `CreateHandArea`，卡槽使用高光 Sprite、内嵌 Glow 层与阴影，满足“发光卡框”要求。  
   - 功能按钮提到 440px 宽度，开启 TMP Auto Sizing + 禁止换行，避免“测试战斗系统”等长文本被截断。
3. **牌堆/弃牌可视化**  
   - 桌面壳体里额外生成垛状/扇形叠牌展示，主题切换时由 `ApplyTableTheme` 批量更新贴图与色调。  
   - `CreateDeckZonesPanel` 调整自适配尺寸，避免在窄壳体内溢出。
4. **透视相机落地**  
   - `CreateCanvas` 改为 Screen Space - Camera，专用 `DeckUICamera` 采用 38° 视场与俯角，配合 RectTransform 的倾斜角实现桌面纵深。

## 风险 & 踩坑
- **自动化路径**：`DeckZoneRoot`/`DiscardStackAnchor` 等锚点保持名称不变，否则 Automation JSON 需要同步调整。  
- **主题刷新**：手牌 Glow、牌堆壳体等新增节点都依赖 `ApplyTableTheme`，忘记维护会出现素材与色调错位。  
- **摄像机遮罩**：Canvas 默认 Layer 仍可能是 Default，`DeckUICamera` 的 cullingMask 需包含 `UI` 与 `Default` 才能看到全部元素。

## 后续建议
- 将桌面骨架导出为 Prefab，`DeckUiTableBuilder` 可作为运行时校正器，减轻每次脚本大改。  
- 记录自动化截图基线，确认新透视镜头不会导致定位误差；必要时更新按钮/卡槽的世界坐标容差。  
- 探索把 Glow/Outline 切换到 UIEffect 材质，减少多重 Image 带来的 Draw Call。
