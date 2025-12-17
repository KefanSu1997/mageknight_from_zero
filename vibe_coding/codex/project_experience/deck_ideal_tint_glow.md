# 理想卡组紫银主题调优笔记

- 主题色快速统一：将 DeckUiThemeCache 默认色改成紫（背景/边框）+紫宝石（Accent）+蓝辉光（Secondary），避免蓝金老主题和参考图冲突。
- 双层边框去重：生成/运行时统一禁用 DeckUiBootstrapper 的基础 Border/BorderHighlight，再叠加自定义银色框，防止截图出现双框重影。
- 卡背堆与漂浮卡：给 CardBack 统一紫色 tint（堆叠用 DeckStackTint/DiscardStackTint，漂浮卡用 0.8/0.6/1），透明度递增保证纵深感。
- 截图多目标写入：BuildAndCapture 一次渲染后同时写入 artifacts 与 runs 目录，减少遗漏验收路径的风险。
- 运行时补偿：DeckIdealRuntimeTuner 覆盖锚点、透明度与缺失图层（光晕、宝石、顶置立绘 glow），即使未重建场景也能贴近参考版面。
