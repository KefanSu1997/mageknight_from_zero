# DeckMana 旧 Canvas 遮挡处理

## 症状
- 运行 `Part1_DeckManaTest` 时仍出现旧版 DeckUiBootstrapper 生成的浮层，导致新版 `Part1SceneHarness` 布局完全不可见。
- 自动化截图使用新版布局，因此手动验证与自动化结果不一致。

## 根因
- 场景中保留了执行 `ExecuteAlways` 的 `DeckUiBootstrapper`，其 Canvas 采用 `ScreenSpaceOverlay` 渲染，优先级高于 `Part1SceneHarness` 在运行时新建的 `ScreenSpaceCamera` Canvas。
- 由于我们只清理了新建 Canvas 下的遗留节点，未禁用旧 Canvas，手动运行时仍渲染旧布局。

## 解决方案
1. 在 `Part1SceneHarness.CreateCanvas()` 前调用 `DisableLegacyDeckUiCanvases()`：
   - 通过 `FindObjectsByType<DeckUiBootstrapper>` 找到所有遗留浮层。
   - 记录并禁用其 `Canvas.enabled`，避免渲染遮挡。
2. 在 `OnDestroy()` 中恢复这些 Canvas 的 `enabled` 状态，确保退出 Play 模式后编辑器层级不受影响。
3. 维持自动化环境不变——运行时始终以新版桌面布局为主。

## 验证
- `Assets/Refresh` 重新编译后 Console 0 报错。
- 禁用旧 Canvas 后，手动运行应与自动化截图完全一致（新版左侧控制栏 + 信息面板可见）。

## 延伸建议
- 若未来彻底弃用 `DeckUiBootstrapper`，可将其从场景中清理或改为编辑器专用脚本。
- 其它测试场景若同时存在旧 Canvas，可复用该禁用/恢复逻辑，保持布局一致性。
