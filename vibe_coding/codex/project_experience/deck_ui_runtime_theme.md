# Deck 测试场景运行时主题生成心得

## 背景
- 需求：在没有现成美术素材的前提下，复刻紫色魔幻牌桌氛围，并保持与现有 Deck 测试脚本的兼容。
- 约束：无法使用 Unity 编辑器直接排版，只能通过脚本和 YAML 修改；需兼顾 `HandManager` 与 `HandManagerAdvanced` 两套逻辑。

## 关键做法
1. **运行时建树**：编写 `DeckUiBootstrapper`，在 `Canvas` 下程序化生成背景、边框、魔法阵、顶部牌堆与底部卡槽，避免手工修改 `.unity` 的大量结构。
2. **自适配布局**：统一调整 `CanvasScaler` 为 `ScaleWithScreenSize`，底部使用 `HorizontalLayoutGroup` 生成卡槽，`handRoot` 在运行时自动重定向到新容器。
3. **临时占位策略**：通过运行时创建的 1x1 `Sprite` 作为色块，占位背景/边框/卡背，简化无素材时的调试，并预留 Sprite 字段方便后续替换正式资源。
4. **动效轻量化**：利用 `DeckUiSineFloat` 和 `DeckUiRotateGraphic` 在不依赖外部插件的情况下实现漂浮与旋转效果，可在有 DOTween/UIEffect 后替换。

## 风险与提醒
- 记得在所有需要的测试场景中挂载 `DeckUiBootstrapper`，否则旧的 `handRoot` 仍指向原始空容器，卡牌会堆在画面中央。
- 当正式美术素材到位时，需要在 Inspector 中替换 `backgroundSprite` 等字段，或扩展脚本支持 ScriptableObject 主题配置。
- 运行时生成的 UI 会在场景保存前常驻层级视图，如不希望编辑态保留，可加 `Application.isPlaying` 判断，但需保证测试模式下仍会创建。

## 后续建议
- 引入 Glow/Outline 专用材质后，替换当前单纯色块的高亮层，提升视觉质量。
- 若要支撑自动化截图比对，可为关键元素打上 `name` 标签，便于脚本定位。

## 2025-10-18 更新：正式美术替换要点
- 通过即梦脚本批量生成星云背景、雕花边框、魔法阵、卡背与高光素材，统一放入 `Assets/UI/Images/DeckTheme/` 方便引用。
- 纹理导入需切换为 Sprite，边框/高光/法阵设置 `AlphaIsTransparency=true` 且 `grayScaleToAlpha=1`，即可把黑底转换为透明通道。
- `DeckUiBootstrapper` 的 `CreateImage` 逻辑新增“有图时默认用白色，只有需要着色的高光层才继续用色值”，避免正式素材被二次染色。
- `Part1_DeckManaTest`、`Part1_CombatTest`、`Part1_ExplorationRecruitmentTest`、`Part1_FullFlowTest` 统一挂载 `DeckUiBootstrapper`，测试场景之间的视觉保持一致，也方便自动化截图比对。
