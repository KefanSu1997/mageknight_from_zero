# 工作记录
- 时间: 2026-01-26_23-09-26
- 任务: 修复 MCP Http Bridge 自动启动卡死；默认取消 DeckIdeal 自动截图

## 思路与计划
- 恢复 MCP Http Bridge 自动启动，但避免主线程阻塞
- 用 async/await + 超时保护替代同步等待
- DeckIdeal 自动截图加开关，默认关闭，避免启动即执行
- 按标准流程检查编译错误

## 具体修改
- 新增: `Assets/Editor/McpHttpBridgeAutoStart.cs`
  - 改为异步执行 Start/Verify/Stop，避免 EditorApplication.update 阻塞
  - 增加 5s 超时保护，避免卡死
- 更新: `Assets/Editor/DeckIdealAutoCaptureOnce.cs`
  - 增加 EditorPrefs 开关 `MageKnight.SceneAutomation.DeckIdealAutoCaptureOnce`
  - 默认关闭自动截图，仅在开启时响应哨兵文件

## 结果
- 启动时 MCP 自动启动不再阻塞主线程（预期）
- DeckIdeal 自动截图默认不再触发（需显式开启）
- 编译错误检查通过（error=0）

## 编译检查
- 复查时间: 2026-01-26_23-09-26
- Console error: 0
