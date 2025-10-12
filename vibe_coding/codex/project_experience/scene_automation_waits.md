# Scene Automation Waits 调优记录

## 2025-09-27
- `SceneAutomationStep.waitAfterSeconds` 改为以 -1 表示“使用配置默认值”，便于在 JSON 中设置逐步等待。
- `SceneAutomationOrchestrator` 改为使用 `WaitForSecondsRealtime` 等待初始延迟与步骤间隔，不再依赖 `Time.timeScale`。
- 自动化配置中适当插入 0.3~0.6s 的等待，可以防止截图捕捉到 UI 尚未稳定的帧。
- 如果截图仍出现 UI 元素缺失，优先排查 Canvas 排序或锚点布局，必要时延长对应步骤的等待时间。
