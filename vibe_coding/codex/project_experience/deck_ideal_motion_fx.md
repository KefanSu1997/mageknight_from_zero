# DeckIdeal 呼吸光效与帧内装饰要点

- 目标：让 Part1_DeckIdeal 贴近紫银华丽召唤感，补齐四角宝石、卷草银框、顶部立绘光晕与呼吸/旋转动效。
- 关键脚本：
  - `DeckUiBreathingScale`：为魔法阵、粒子、银框贴上轻微缩放呼吸。
  - `DeckUiSlowRotate`：给光圈/粒子做缓慢旋转，避免死图。
  - `DeckUiGlowPulse`：继续承担高光明灭，叠加在 filigree、宝石、卡牌金边上。
- 场景生成：`DeckIdealSceneBuilder` 新增 FrameFiligree/FrameShadow、CornerGems、MagicAura、TopCreature 呼吸+漂浮，左右牌堆差异化透明度与轻浮动画，底部 5 张角色卡增加金色 filigree。
- 运行时兜底：`DeckIdealRuntimeTuner` 自动补齐/上色银框、卷草 filigree、角落宝石、顶部立绘与魔法光晕，并为魔法阵/粒子/堆叠添加呼吸和缓转，保证即便未重建场景也呈现完整特效。
- 调试提示：在 Scene 视图勾选 “Animated Materials” 可实时观察呼吸/旋转；若某层缺失，检查节点名称是否匹配（SilverFrameOverlay、FrameFiligree、MagicAura、TopCreature 等）。
